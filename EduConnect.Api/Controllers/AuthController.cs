using EduConnect.Api.Data;
using EduConnect.Api.Domain;
using EduConnect.Api.DTOs;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MimeKit;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace EduConnect.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly PasswordHasher<Usuario> _hasher = new();

    public AuthController(AppDbContext db, IConfiguration config, IWebHostEnvironment env)
    {
        _db = db;
        _config = config;
        _env = env;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var matricula = dto.Matricula.Trim();

        Usuario? user = null;

        if (matricula == "0001")
        {
            user = await _db.Usuarios.FirstOrDefaultAsync(u => u.Perfil == PerfilUsuario.ADMIN);
        }
        else if (matricula.StartsWith("A"))
        {
            var aluno = await _db.Alunos.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Matricula == matricula);

            if (aluno is null) return Unauthorized("Credenciais inválidas.");

            user = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == aluno.UsuarioId);
        }
        else if (matricula.StartsWith("P"))
        {
            var prof = await _db.Professores.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Matricula == matricula);

            if (prof is null) return Unauthorized("Credenciais inválidas.");

            user = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == prof.UsuarioId);
        }
        else
        {
            return Unauthorized("Credenciais inválidas.");
        }

        if (user is null) return Unauthorized("Credenciais inválidas.");

        if (!user.Ativo) return Unauthorized("Usuário inativo.");

        var result = _hasher.VerifyHashedPassword(user, user.SenhaHash, dto.Senha);
        if (result == PasswordVerificationResult.Failed) return Unauthorized("Credenciais inválidas.");

        var token = GerarToken(user);

        return Ok(new
        {
            token,
            user.Id,
            user.Nome,
            perfil = user.Perfil.ToString()
        });
    }

    // PRIMEIRO ACESSO (CPF + EMAIL) -> envia matrícula + senha provisória
    [HttpPost("primeiro-acesso")]
    public async Task<IActionResult> PrimeiroAcesso([FromBody] PrimeiroAcessoDto dto)
    {
        // resposta neutra (não vaza se usuário existe)
        const string okMsg = "Se os dados estiverem corretos, você receberá um e-mail com matrícula e senha provisória.";

        var cpf = (dto.Cpf ?? "").Trim();
        var email = (dto.Email ?? "").Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(cpf) || string.IsNullOrWhiteSpace(email))
            return BadRequest("CPF e Email são obrigatórios.");

        cpf = SomenteDigitos(cpf);

        // precisa existir: Usuario.Cpf (string)
        var user = await _db.Usuarios
            .Include(u => u.Aluno)
            .Include(u => u.Professor)
            .FirstOrDefaultAsync(u => u.Cpf == cpf);

        if (user is null || !user.Ativo)
            return Ok(okMsg);

        if (!string.Equals(user.Email?.Trim(), email, StringComparison.OrdinalIgnoreCase))
            return Ok(okMsg);

        string? matricula = null;

        if (user.Perfil == PerfilUsuario.ADMIN)
        {
            matricula = "0001";
        }
        else if (user.Perfil == PerfilUsuario.ALUNO)
        {
            matricula = user.Aluno?.Matricula;
        }
        else if (user.Perfil == PerfilUsuario.PROFESSOR)
        {
            matricula = user.Professor?.Matricula;
        }

        if (string.IsNullOrWhiteSpace(matricula))
            return Ok(okMsg);

        var senhaProvisoria = GerarSenhaProvisoria(10);
        user.SenhaHash = _hasher.HashPassword(user, senhaProvisoria);

        // Depois você cria o endpoint de troca de senha e pode marcar flag aqui
        // user.PrecisaTrocarSenha = true;

        await _db.SaveChangesAsync();

        var subject = "EduConnect - Primeiro acesso";
        var body = $@"
<p>Olá, {System.Net.WebUtility.HtmlEncode(user.Nome)}.</p>
<p>Para acessar o sistema pela primeira vez, utilize as seguintes credenciais:</p>
<p><b>Matrícula:</b> {System.Net.WebUtility.HtmlEncode(matricula)}<br/>
<b>Senha provisória:</b> {System.Net.WebUtility.HtmlEncode(senhaProvisoria)}</p>
<p>Ao entrar, altere sua senha.</p>";

        await EnviarEmailGmailAsync(user.Email, subject, body);

        return Ok(okMsg);
    }

    [HttpPost("bootstrap-admin")]
    public async Task<IActionResult> BootstrapAdmin(AdminBootstrapDto dto)
    {
        // Bloqueia fora de Development
        if (!_env.IsDevelopment())
            return NotFound();

        var adminExiste = await _db.Usuarios.AnyAsync(u => u.Perfil == PerfilUsuario.ADMIN);
        if (adminExiste) return BadRequest("Admin já existe.");

        if (string.IsNullOrWhiteSpace(dto.Nome)) return BadRequest("Nome é obrigatório.");
        if (string.IsNullOrWhiteSpace(dto.Email)) return BadRequest("Email é obrigatório.");
        if (string.IsNullOrWhiteSpace(dto.Senha)) return BadRequest("Senha é obrigatória.");

        var email = dto.Email.Trim().ToLower();

        if (await _db.Usuarios.AnyAsync(u => u.Email == email))
            return BadRequest("Email já cadastrado.");

        var admin = new Usuario
        {
            Nome = dto.Nome.Trim(),
            Email = email,
            Perfil = PerfilUsuario.ADMIN,
            Ativo = true
        };

        admin.SenhaHash = _hasher.HashPassword(admin, dto.Senha);

        _db.Usuarios.Add(admin);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            matricula = "0001",
            admin.Id,
            admin.Nome,
            admin.Email
        });
    }

    // ESQUECI MINHA SENHA (email) -> envia senha provisória
    [HttpPost("esqueci-senha")]
    public async Task<IActionResult> EsqueciSenha([FromBody] EsqueciSenhaDto dto)
    {
        // resposta neutra (não vaza se email existe)
        const string okMsg = "Se o e-mail estiver cadastrado, você receberá uma nova senha provisória.";

        var email = (dto.Email ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest("Email é obrigatório.");

        var user = await _db.Usuarios
            .Include(u => u.Aluno)
            .Include(u => u.Professor)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email);

        if (user is null || !user.Ativo)
            return Ok(okMsg);

        // Descobre matrícula (para o email ficar útil)
        string? matricula = null;

        if (user.Perfil == PerfilUsuario.ADMIN)
            matricula = "0001";
        else if (user.Perfil == PerfilUsuario.ALUNO)
            matricula = user.Aluno?.Matricula;
        else if (user.Perfil == PerfilUsuario.PROFESSOR)
            matricula = user.Professor?.Matricula;

        // Gera senha provisória
        var senhaProvisoria = GerarSenhaProvisoria(10);

        // Salva hash
        user.SenhaHash = _hasher.HashPassword(user, senhaProvisoria);
        await _db.SaveChangesAsync();

        var subject = "EduConnect - Recuperação de senha";

        var body = $@"
<p>Olá, {System.Net.WebUtility.HtmlEncode(user.Nome)}.</p>
<p>Foi solicitada a recuperação de senha do EduConnect.</p>
<p><b>Matrícula:</b> {System.Net.WebUtility.HtmlEncode(matricula ?? "-")}<br/>
<b>Nova senha provisória:</b> {System.Net.WebUtility.HtmlEncode(senhaProvisoria)}</p>
<p>Faça login e altere sua senha.</p>";

        await EnviarEmailGmailAsync(user.Email, subject, body);

        return Ok(okMsg);
    }


    // ALTERAR SENHA (autenticado) -> nova senha + confirmação
    [Authorize]
    [HttpPost("alterar-senha")]
    public async Task<IActionResult> AlterarSenha([FromBody] AlterarSenhaDto dto)
    {
        var novaSenha = (dto.NovaSenha ?? "").Trim();
        var confirmacao = (dto.Confirmacao ?? "").Trim();

        if (string.IsNullOrWhiteSpace(novaSenha) || string.IsNullOrWhiteSpace(confirmacao))
            return BadRequest("NovaSenha e Confirmacao são obrigatórios.");

        if (novaSenha != confirmacao)
            return BadRequest("Confirmação de senha não confere.");

        if (!SenhaForte(novaSenha))
            return BadRequest("Senha inválida: mínimo 8 caracteres, com letras maiúsculas, minúsculas e ao menos 1 número.");

        // pega userId do token
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var user = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return Unauthorized();
        if (!user.Ativo) return Unauthorized("Usuário inativo.");

        user.SenhaHash = _hasher.HashPassword(user, novaSenha);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private static bool SenhaForte(string senha)
    {
        if (senha.Length < 8) return false;

        bool hasUpper = senha.Any(char.IsUpper);
        bool hasLower = senha.Any(char.IsLower);
        bool hasDigit = senha.Any(char.IsDigit);

        return hasUpper && hasLower && hasDigit;
    }

    private string GerarToken(Usuario user)
    {
        var key = _config["Jwt:Key"]!;
        var issuer = _config["Jwt:Issuer"]!;
        var audience = _config["Jwt:Audience"]!;
        var expiresMinutes = int.Parse(_config["Jwt:ExpiresMinutes"]!);

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Role, user.Perfil.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task EnviarEmailGmailAsync(string toEmail, string subject, string htmlBody)
    {
        // usa appsettings.json -> "Smtp"
        var host = _config["Smtp:Host"] ?? "smtp.gmail.com";
        var port = int.TryParse(_config["Smtp:Port"], out var p) ? p : 587;
        var user = _config["Smtp:User"] ?? throw new InvalidOperationException("Smtp:User não configurado");
        var pass = _config["Smtp:Pass"] ?? throw new InvalidOperationException("Smtp:Pass não configurado");
        var fromName = _config["Smtp:FromName"] ?? "EduConnect";

        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(fromName, user));
        msg.To.Add(MailboxAddress.Parse(toEmail));
        msg.Subject = subject;
        msg.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(host, port, SecureSocketOptions.StartTls);
        await smtp.AuthenticateAsync(user, pass);
        await smtp.SendAsync(msg);
        await smtp.DisconnectAsync(true);
    }

    private static string SomenteDigitos(string s)
        => new string((s ?? "").Where(char.IsDigit).ToArray());

    private static string GerarSenhaProvisoria(int length = 10)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#";
        var bytes = RandomNumberGenerator.GetBytes(length);
        var result = new char[length];
        for (int i = 0; i < length; i++)
            result[i] = chars[bytes[i] % chars.Length];
        return new string(result);
    }
}


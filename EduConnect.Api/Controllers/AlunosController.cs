using EduConnect.Api.Data;
using EduConnect.Api.Domain;
using EduConnect.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EduConnect.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AlunosController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher<Usuario> _hasher = new();

    public AlunosController(AppDbContext db) => _db = db;

    // ADMIN: lista todos
    [Authorize(Roles = "ADMIN")]
    [HttpGet]
    public async Task<ActionResult<List<AlunoReadDto>>> GetAll([FromQuery] bool includeInativos = false)
    {
        var query = _db.Alunos
            .AsNoTracking()
            .Include(a => a.Usuario)
            .AsQueryable();

        if (!includeInativos)
            query = query.Where(a => a.Usuario.Ativo);

        var alunos = await query
            .Select(a => new AlunoReadDto(
                a.Id,
                a.Matricula,
                a.UsuarioId,
                a.Usuario.Nome,
                a.Usuario.Email
            ))
            .ToListAsync();

        return Ok(alunos);
    }

    // ADMIN: busca por id
    [Authorize(Roles = "ADMIN")]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<AlunoReadDto>> GetById(int id)
    {
        var aluno = await _db.Alunos
            .AsNoTracking()
            .Include(a => a.Usuario)
            .Where(a => a.Id == id)
            .Select(a => new AlunoReadDto(
                a.Id,
                a.Matricula,
                a.UsuarioId,
                a.Usuario.Nome,
                a.Usuario.Email
            ))
            .FirstOrDefaultAsync();

        if (aluno is null) return NotFound();
        return Ok(aluno);
    }

    // ALUNO: consulta os próprios dados
    [Authorize(Roles = "ALUNO")]
    [HttpGet("me")]
    public async Task<ActionResult<AlunoReadDto>> GetMe()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        var aluno = await _db.Alunos
            .AsNoTracking()
            .Include(a => a.Usuario)
            .Where(a => a.UsuarioId == userId)
            .Select(a => new AlunoReadDto(
                a.Id,
                a.Matricula,
                a.UsuarioId,
                a.Usuario.Nome,
                a.Usuario.Email
            ))
            .FirstOrDefaultAsync();

        if (aluno is null) return NotFound();
        return Ok(aluno);
    }

    // ADMIN: cria aluno + usuário + matrícula automática
    [Authorize(Roles = "ADMIN")]
    [HttpPost]
    public async Task<ActionResult<AlunoReadDto>> Create(AlunoCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome)) return BadRequest("Nome é obrigatório.");
        if (string.IsNullOrWhiteSpace(dto.Email)) return BadRequest("Email é obrigatório.");
        if (string.IsNullOrWhiteSpace(dto.Senha)) return BadRequest("Senha é obrigatória.");
        if (string.IsNullOrWhiteSpace(dto.Cpf)) return BadRequest("CPF é obrigatório.");

        var email = dto.Email.Trim().ToLower();
        var cpf = OnlyDigits(dto.Cpf);

        if (cpf.Length != 11) return BadRequest("CPF inválido.");

        if (await _db.Usuarios.AnyAsync(x => x.Email == email))
            return BadRequest("Email já cadastrado.");

        if (await _db.Usuarios.AnyAsync(x => x.Cpf == cpf))
            return BadRequest("CPF já cadastrado.");

        using var tx = await _db.Database.BeginTransactionAsync();

        var usuario = new Usuario
        {
            Nome = dto.Nome.Trim(),
            Email = email,
            Cpf = cpf,
            Perfil = PerfilUsuario.ALUNO,
            Ativo = true
        };
        usuario.SenhaHash = _hasher.HashPassword(usuario, dto.Senha);

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        var matricula = await GerarMatriculaAlunoAsync(); // A000001...

        var alunoEntity = new Aluno
        {
            UsuarioId = usuario.Id,
            Matricula = matricula
        };

        _db.Alunos.Add(alunoEntity);
        await _db.SaveChangesAsync();

        await tx.CommitAsync();

        var read = new AlunoReadDto(alunoEntity.Id, alunoEntity.Matricula, usuario.Id, usuario.Nome, usuario.Email);
        return CreatedAtAction(nameof(GetById), new { id = alunoEntity.Id }, read);
    }

    private async Task<string> GerarMatriculaAlunoAsync()
    {
        var ultima = await _db.Alunos
            .AsNoTracking()
            .Where(a => a.Matricula.StartsWith("A"))
            .OrderByDescending(a => a.Matricula)
            .Select(a => a.Matricula)
            .FirstOrDefaultAsync();

        var proximoNumero = 1;

        if (!string.IsNullOrWhiteSpace(ultima) && ultima.Length >= 2)
        {
            var parteNumerica = ultima[1..]; // remove 'A'
            if (int.TryParse(parteNumerica, out var n))
                proximoNumero = n + 1;
        }

        return $"A{proximoNumero:D6}";
    }

    // ADMIN: atualiza nome/email/ativo do aluno (via Usuario)
    [Authorize(Roles = "ADMIN")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, AlunoUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome)) return BadRequest("Nome é obrigatório.");
        if (string.IsNullOrWhiteSpace(dto.Email)) return BadRequest("Email é obrigatório.");

        var aluno = await _db.Alunos
            .Include(a => a.Usuario)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (aluno is null) return NotFound();

        var email = dto.Email.Trim().ToLower();

        // evita duplicar email em outro usuário
        var emailEmUso = await _db.Usuarios.AnyAsync(u => u.Email == email && u.Id != aluno.UsuarioId);
        if (emailEmUso) return BadRequest("Email já cadastrado.");

        aluno.Usuario.Nome = dto.Nome.Trim();
        aluno.Usuario.Email = email;
        aluno.Usuario.Ativo = dto.Ativo;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ADMIN: "delete" lógico (desativa usuário)
    [Authorize(Roles = "ADMIN")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var aluno = await _db.Alunos
            .Include(a => a.Usuario)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (aluno is null) return NotFound();

        aluno.Usuario.Ativo = false;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private static string OnlyDigits(string s) => new string((s ?? "").Where(char.IsDigit).ToArray());

}

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
public class ProfessoresController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher<Usuario> _hasher = new();

    public ProfessoresController(AppDbContext db) => _db = db;

    // ADMIN: lista todos
    [Authorize(Roles = "ADMIN")]
    [HttpGet]
    public async Task<ActionResult<List<ProfessorReadDto>>> GetAll([FromQuery] bool includeInativos = false)
    {
        var query = _db.Professores
            .AsNoTracking()
            .Include(p => p.Usuario)
            .AsQueryable();

        if (!includeInativos)
            query = query.Where(p => p.Usuario.Ativo);

        var professores = await query
            .Select(p => new ProfessorReadDto(
                p.Id,
                p.Matricula,
                p.UsuarioId,
                p.Usuario.Nome,
                p.Usuario.Email
            ))
            .ToListAsync();

        return Ok(professores);
    }

    // ADMIN: busca por id
    [Authorize(Roles = "ADMIN")]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProfessorReadDto>> GetById(int id)
    {
        var prof = await _db.Professores
            .AsNoTracking()
            .Include(p => p.Usuario)
            .Where(p => p.Id == id)
            .Select(p => new ProfessorReadDto(
                p.Id,
                p.Matricula,
                p.UsuarioId,
                p.Usuario.Nome,
                p.Usuario.Email
            ))
            .FirstOrDefaultAsync();

        if (prof is null) return NotFound();
        return Ok(prof);
    }

    // PROFESSOR: consulta os próprios dados
    [Authorize(Roles = "PROFESSOR")]
    [HttpGet("me")]
    public async Task<ActionResult<ProfessorReadDto>> GetMe()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        var prof = await _db.Professores
            .AsNoTracking()
            .Include(p => p.Usuario)
            .Where(p => p.UsuarioId == userId)
            .Select(p => new ProfessorReadDto(
                p.Id,
                p.Matricula,
                p.UsuarioId,
                p.Usuario.Nome,
                p.Usuario.Email
            ))
            .FirstOrDefaultAsync();

        if (prof is null) return NotFound();
        return Ok(prof);
    }

    // ADMIN: cria professor + usuário + matrícula automática
    [Authorize(Roles = "ADMIN")]
    [HttpPost]
    public async Task<ActionResult<ProfessorReadDto>> Create(ProfessorCreateDto dto)
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
            Perfil = PerfilUsuario.PROFESSOR,
            Ativo = true
        };

        usuario.SenhaHash = _hasher.HashPassword(usuario, dto.Senha);

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        var matricula = await GerarMatriculaProfessorAsync(); // P000001...

        var professorEntity = new Professor
        {
            UsuarioId = usuario.Id,
            Matricula = matricula
        };

        _db.Professores.Add(professorEntity);
        await _db.SaveChangesAsync();

        await tx.CommitAsync();

        var read = new ProfessorReadDto(professorEntity.Id, professorEntity.Matricula, usuario.Id, usuario.Nome, usuario.Email);
        return CreatedAtAction(nameof(GetById), new { id = professorEntity.Id }, read);
    }

    private async Task<string> GerarMatriculaProfessorAsync()
    {
        var ultima = await _db.Professores
            .AsNoTracking()
            .Where(p => p.Matricula.StartsWith("P"))
            .OrderByDescending(p => p.Matricula)
            .Select(p => p.Matricula)
            .FirstOrDefaultAsync();

        var proximoNumero = 1;

        if (!string.IsNullOrWhiteSpace(ultima) && ultima.Length >= 2)
        {
            var parteNumerica = ultima[1..]; // remove 'P'
            if (int.TryParse(parteNumerica, out var n))
                proximoNumero = n + 1;
        }

        return $"P{proximoNumero:D6}";
    }

    // ADMIN: atualiza nome/email/ativo do professor (via Usuario)
    [Authorize(Roles = "ADMIN")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ProfessorUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome)) return BadRequest("Nome é obrigatório.");
        if (string.IsNullOrWhiteSpace(dto.Email)) return BadRequest("Email é obrigatório.");

        var prof = await _db.Professores
            .Include(p => p.Usuario)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (prof is null) return NotFound();

        var email = dto.Email.Trim().ToLower();

        var emailEmUso = await _db.Usuarios.AnyAsync(u => u.Email == email && u.Id != prof.UsuarioId);
        if (emailEmUso) return BadRequest("Email já cadastrado.");

        prof.Usuario.Nome = dto.Nome.Trim();
        prof.Usuario.Email = email;
        prof.Usuario.Ativo = dto.Ativo;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ADMIN: "delete" lógico (desativa usuário)
    [Authorize(Roles = "ADMIN")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var prof = await _db.Professores
            .Include(p => p.Usuario)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (prof is null) return NotFound();

        prof.Usuario.Ativo = false;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private static string OnlyDigits(string s) => new string((s ?? "").Where(char.IsDigit).ToArray());
}

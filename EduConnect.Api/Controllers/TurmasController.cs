using EduConnect.Api.Data;
using EduConnect.Api.Domain;
using EduConnect.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EduConnect.Api.Controllers;

[Authorize] // <-- mudou: agora só exige estar autenticado
[ApiController]
[Route("api/[controller]")]
public class TurmasController : ControllerBase
{
    private readonly AppDbContext _db;

    public TurmasController(AppDbContext db) => _db = db;

    [Authorize(Roles = "ADMIN")]
    [HttpGet]
    public async Task<ActionResult<List<TurmaReadDto>>> GetAll([FromQuery] bool includeInativas = false)
    {
        var query = _db.Turmas.AsNoTracking().AsQueryable();

        if (!includeInativas)
            query = query.Where(t => t.Ativa);

        var turmas = await query
            .OrderByDescending(t => t.Ano).ThenByDescending(t => t.Semestre)
            .Select(t => new TurmaReadDto(t.Id, t.Nome, t.Ano, t.Semestre, t.Periodo.ToString(), t.Ativa))
            .ToListAsync();

        return Ok(turmas);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPost]
    public async Task<ActionResult<TurmaReadDto>> Create(TurmaCreateDto dto)
    {
        if (dto.Ano < 2000 || dto.Ano > 2100) return BadRequest("Ano inválido.");
        if (dto.Semestre is not (1 or 2)) return BadRequest("Semestre deve ser 1 ou 2.");

        var existe = await _db.Turmas.AnyAsync(t =>
            t.Ano == dto.Ano &&
            t.Semestre == dto.Semestre &&
            t.Periodo == dto.Periodo);

        if (existe) return BadRequest("Já existe uma turma com esse ano/semestre/período.");

        var codigo = GerarCodigoTurma(dto.Ano, dto.Semestre, dto.Periodo);

        var turma = new Turma
        {
            Nome = codigo,
            Ano = dto.Ano,
            Semestre = dto.Semestre,
            Periodo = dto.Periodo,
            Ativa = true
        };

        _db.Turmas.Add(turma);
        await _db.SaveChangesAsync();

        return Ok(new TurmaReadDto(
            turma.Id,
            turma.Nome,
            turma.Ano,
            turma.Semestre,
            turma.Periodo.ToString(),
            turma.Ativa
        ));
    }

    private static string GerarCodigoTurma(int ano, byte semestre, PeriodoTurma periodo)
    {
        var yy = (ano % 100).ToString("D2");
        var letra = periodo switch
        {
            PeriodoTurma.MATUTINO => "M",
            PeriodoTurma.VESPERTINO => "V",
            PeriodoTurma.NOTURNO => "N",
            _ => "X"
        };

        return $"{yy}{semestre}-{letra}";
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPost("{turmaId:int}/alunos")]
    public async Task<IActionResult> VincularAluno(int turmaId, VincularAlunoDto dto)
    {
        var turmaExiste = await _db.Turmas.AnyAsync(t => t.Id == turmaId);
        if (!turmaExiste) return NotFound("Turma não encontrada.");

        var alunoExiste = await _db.Alunos.AnyAsync(a => a.Id == dto.AlunoId);
        if (!alunoExiste) return NotFound("Aluno não encontrado.");

        var jaAtivo = await _db.TurmaAlunos.AnyAsync(x =>
            x.TurmaId == turmaId && x.AlunoId == dto.AlunoId && x.Ativo);

        if (jaAtivo) return BadRequest("Aluno já está vinculado a essa turma.");

        _db.TurmaAlunos.Add(new Domain.TurmaAluno
        {
            TurmaId = turmaId,
            AlunoId = dto.AlunoId,
            Ativo = true
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Roles = "ADMIN,PROFESSOR")]
    [HttpGet("{turmaId:int}/alunos")]
    public async Task<IActionResult> ListarAlunos(int turmaId, [FromQuery] bool includeInativos = false)
    {
        var turmaExiste = await _db.Turmas.AnyAsync(t => t.Id == turmaId);
        if (!turmaExiste) return NotFound("Turma não encontrada.");

        var role = User.FindFirstValue(ClaimTypes.Role);

        // Se for PROFESSOR: só pode listar se estiver vinculado à turma
        if (role == "PROFESSOR")
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

            var prof = await _db.Professores.AsNoTracking()
                .FirstOrDefaultAsync(p => p.UsuarioId == userId);

            if (prof is null) return Unauthorized();

            var vinculado = await _db.TurmaProfessores.AsNoTracking()
                .AnyAsync(x => x.TurmaId == turmaId && x.ProfessorId == prof.Id && x.Ativo);

            if (!vinculado) return Forbid();

            // professor não deve enxergar vínculos inativos
            includeInativos = false;
        }

        var query = _db.TurmaAlunos
            .AsNoTracking()
            .Where(x => x.TurmaId == turmaId);

        if (!includeInativos)
            query = query.Where(x => x.Ativo);

        var result = await query
            .Join(_db.Alunos.AsNoTracking(), ta => ta.AlunoId, a => a.Id, (ta, a) => new { ta, a })
            .Join(_db.Usuarios.AsNoTracking(), x => x.a.UsuarioId, u => u.Id, (x, u) => new
            {
                x.a.Id,
                x.a.Matricula,
                Nome = u.Nome,
                Email = u.Email,
                AtivoVinculo = x.ta.Ativo
            })
            .OrderBy(x => x.Matricula)
            .ToListAsync();

        return Ok(result);
    }


    [Authorize(Roles = "ADMIN")]
    [HttpDelete("{turmaId:int}/alunos/{alunoId:int}")]
    public async Task<IActionResult> DesvincularAluno(int turmaId, int alunoId)
    {
        var vinculo = await _db.TurmaAlunos
            .FirstOrDefaultAsync(x => x.TurmaId == turmaId && x.AlunoId == alunoId && x.Ativo);

        if (vinculo is null) return NotFound("Vínculo não encontrado.");

        vinculo.Ativo = false;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPost("{turmaId:int}/professores")]
    public async Task<IActionResult> VincularProfessor(int turmaId, VincularProfessorDto dto)
    {
        var turmaExiste = await _db.Turmas.AnyAsync(t => t.Id == turmaId);
        if (!turmaExiste) return NotFound("Turma não encontrada.");

        var profExiste = await _db.Professores.AnyAsync(p => p.Id == dto.ProfessorId);
        if (!profExiste) return NotFound("Professor não encontrado.");

        var jaAtivo = await _db.TurmaProfessores.AnyAsync(x =>
            x.TurmaId == turmaId && x.ProfessorId == dto.ProfessorId && x.Ativo);

        if (jaAtivo) return BadRequest("Professor já está vinculado a essa turma.");

        _db.TurmaProfessores.Add(new Domain.TurmaProfessor
        {
            TurmaId = turmaId,
            ProfessorId = dto.ProfessorId,
            Ativo = true
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Roles = "ADMIN")]
    [HttpGet("{turmaId:int}/professores")]
    public async Task<IActionResult> ListarProfessores(int turmaId, [FromQuery] bool includeInativos = false)
    {
        var turmaExiste = await _db.Turmas.AnyAsync(t => t.Id == turmaId);
        if (!turmaExiste) return NotFound("Turma não encontrada.");

        var query = _db.TurmaProfessores
            .AsNoTracking()
            .Where(x => x.TurmaId == turmaId);

        if (!includeInativos)
            query = query.Where(x => x.Ativo);

        var result = await query
            .Join(_db.Professores.AsNoTracking(), tp => tp.ProfessorId, p => p.Id, (tp, p) => new { tp, p })
            .Join(_db.Usuarios.AsNoTracking(), x => x.p.UsuarioId, u => u.Id, (x, u) => new
            {
                x.p.Id,
                x.p.Matricula,
                Nome = u.Nome,
                Email = u.Email,
                AtivoVinculo = x.tp.Ativo
            })
            .OrderBy(x => x.Matricula)
            .ToListAsync();

        return Ok(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpDelete("{turmaId:int}/professores/{professorId:int}")]
    public async Task<IActionResult> DesvincularProfessor(int turmaId, int professorId)
    {
        var vinculo = await _db.TurmaProfessores
            .FirstOrDefaultAsync(x => x.TurmaId == turmaId && x.ProfessorId == professorId && x.Ativo);

        if (vinculo is null) return NotFound("Vínculo não encontrado.");

        vinculo.Ativo = false;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Roles = "ALUNO,PROFESSOR")]
    [HttpGet("me")]
    public async Task<ActionResult<List<TurmaMeDto>>> GetMe()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = User.FindFirstValue(ClaimTypes.Role);

        if (!int.TryParse(userIdStr, out var userId) || string.IsNullOrWhiteSpace(role))
            return Unauthorized();

        if (role == "ALUNO")
        {
            var turmas = await _db.TurmaAlunos
                .AsNoTracking()
                .Where(x => x.Ativo && x.Aluno.UsuarioId == userId)
                .Select(x => x.Turma)
                .Distinct()
                .OrderBy(t => t.Ano).ThenBy(t => t.Semestre).ThenBy(t => t.Periodo).ThenBy(t => t.Nome)
                .Select(t => new TurmaMeDto(t.Id, t.Nome, t.Ano, t.Semestre, t.Periodo.ToString()))
                .ToListAsync();

            return Ok(turmas);
        }

        if (role == "PROFESSOR")
        {
            var turmas = await _db.TurmaProfessores
                .AsNoTracking()
                .Where(x => x.Ativo && x.Professor.UsuarioId == userId)
                .Select(x => x.Turma)
                .Distinct()
                .OrderBy(t => t.Ano).ThenBy(t => t.Semestre).ThenBy(t => t.Periodo).ThenBy(t => t.Nome)
                .Select(t => new TurmaMeDto(t.Id, t.Nome, t.Ano, t.Semestre, t.Periodo.ToString()))
                .ToListAsync();

            return Ok(turmas);
        }

        return Forbid();
    }
}

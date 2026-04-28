using EduConnect.Api.Data;
using EduConnect.Api.Domain;
using EduConnect.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EduConnect.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class OfertasController : ControllerBase
{
    private readonly AppDbContext _db;
    public OfertasController(AppDbContext db) => _db = db;

    [Authorize(Roles = "ADMIN")]
    [HttpGet]
    public async Task<ActionResult<List<OfertaReadDto>>> GetAll([FromQuery] bool includeInativas = false)
    {
        var query = _db.OfertaDisciplinas
            .AsNoTracking()
            .Include(o => o.Disciplina)
            .Include(o => o.Professor).ThenInclude(p => p.Usuario)
            .Include(o => o.Turma)
            .AsQueryable();

        if (!includeInativas)
            query = query.Where(o => o.Ativa);

        var list = await query
            .OrderByDescending(o => o.Ano).ThenByDescending(o => o.Semestre)
            .ThenBy(o => o.Disciplina.Codigo)
            .Select(o => new OfertaReadDto(
                o.Id,
                o.Ano,
                o.Semestre,
                o.Periodo.ToString(),
                o.Ativa,
                o.DisciplinaId,
                o.Disciplina.Codigo,
                o.Disciplina.Nome,
                o.ProfessorId,
                o.Professor.Matricula,
                o.Professor.Usuario.Nome,
                o.TurmaId,
                o.Turma != null ? o.Turma.Nome : null
            ))
            .ToListAsync();

        return Ok(list);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPost]
    public async Task<ActionResult<OfertaReadDto>> Create(OfertaCreateDto dto)
    {
        if (dto.Ano < 2000 || dto.Ano > 2100) return BadRequest("Ano inválido.");
        if (dto.Semestre is not (1 or 2)) return BadRequest("Semestre deve ser 1 ou 2.");

        var disciplinaOk = await _db.Disciplinas.AnyAsync(d => d.Id == dto.DisciplinaId && d.Ativa);
        if (!disciplinaOk) return BadRequest("Disciplina inválida/inativa.");

        var professorOk = await _db.Professores.AnyAsync(p => p.Id == dto.ProfessorId);
        if (!professorOk) return BadRequest("Professor inválido.");

        if (dto.TurmaId.HasValue)
        {
            var turmaOk = await _db.Turmas.AnyAsync(t => t.Id == dto.TurmaId.Value && t.Ativa);
            if (!turmaOk) return BadRequest("Turma inválida/inativa.");
        }

        var existe = await _db.OfertaDisciplinas.AnyAsync(o =>
            o.DisciplinaId == dto.DisciplinaId &&
            o.ProfessorId == dto.ProfessorId &&
            o.Ano == dto.Ano &&
            o.Semestre == dto.Semestre &&
            o.Periodo == dto.Periodo &&
            o.TurmaId == dto.TurmaId);

        if (existe) return BadRequest("Oferta já existe.");

        var oferta = new OfertaDisciplina
        {
            DisciplinaId = dto.DisciplinaId,
            ProfessorId = dto.ProfessorId,
            TurmaId = dto.TurmaId,
            Ano = dto.Ano,
            Semestre = dto.Semestre,
            Periodo = dto.Periodo,
            Ativa = true
        };

        _db.OfertaDisciplinas.Add(oferta);
        await _db.SaveChangesAsync();

        // retorna via GET (admin)
        var created = await _db.OfertaDisciplinas
            .AsNoTracking()
            .Include(o => o.Disciplina)
            .Include(o => o.Professor).ThenInclude(p => p.Usuario)
            .Include(o => o.Turma)
            .Where(o => o.Id == oferta.Id)
            .Select(o => new OfertaReadDto(
                o.Id, o.Ano, o.Semestre, o.Periodo.ToString(), o.Ativa,
                o.DisciplinaId, o.Disciplina.Codigo, o.Disciplina.Nome,
                o.ProfessorId, o.Professor.Matricula, o.Professor.Usuario.Nome,
                o.TurmaId, o.Turma != null ? o.Turma.Nome : null
            ))
            .FirstAsync();

        return Ok(created);
    }

    // ADMIN: matricular aluno na oferta
    [Authorize(Roles = "ADMIN")]
    [HttpPost("{ofertaId:int}/alunos")]
    public async Task<IActionResult> VincularAluno(int ofertaId, OfertaVincularAlunoDto dto)
    {
        var ofertaExiste = await _db.OfertaDisciplinas.AnyAsync(o => o.Id == ofertaId && o.Ativa);
        if (!ofertaExiste) return NotFound("Oferta não encontrada.");

        var alunoExiste = await _db.Alunos.AnyAsync(a => a.Id == dto.AlunoId);
        if (!alunoExiste) return NotFound("Aluno não encontrado.");

        var jaAtivo = await _db.OfertaAlunos.AnyAsync(x =>
            x.OfertaDisciplinaId == ofertaId && x.AlunoId == dto.AlunoId && x.Ativo);

        if (jaAtivo) return BadRequest("Aluno já está matriculado nessa oferta.");

        _db.OfertaAlunos.Add(new OfertaAluno
        {
            OfertaDisciplinaId = ofertaId,
            AlunoId = dto.AlunoId,
            Ativo = true
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{ofertaId:int}/alunos")]
    [Authorize(Roles = "PROFESSOR,ADMIN")]
    public async Task<ActionResult<PagedResultDto<OfertaAlunoListItemDto>>> ListarAlunos(
    int ofertaId,
    [FromQuery] string? search = null,
    [FromQuery] bool includeInativas = false,
    [FromQuery] int skip = 0,
    [FromQuery] int limit = 50)
    {
        if (skip < 0) skip = 0;
        if (limit <= 0) limit = 50;
        if (limit > 200) limit = 200;

        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        // 1) Base: vínculos (OfertaAluno)
        var oaBase = _db.OfertaAlunos.AsNoTracking()
            .Where(oa => oa.OfertaDisciplinaId == ofertaId);

        if (!includeInativas)
            oaBase = oaBase.Where(oa => oa.Ativo);

        // 2) Se houver search, descobrimos os alunoIds elegíveis
        List<int>? alunoIdsFiltro = null;

        if (search is not null)
        {
            // por matrícula
            var idsPorMatricula = await _db.Alunos.AsNoTracking()
                .Where(a => a.Matricula.Contains(search))
                .Select(a => a.Id)
                .Take(5000)
                .ToListAsync();

            // por nome (via Usuario)
            var userIdsPorNome = await _db.Usuarios.AsNoTracking()
                .Where(u => u.Nome.Contains(search))
                .Select(u => u.Id)
                .Take(5000)
                .ToListAsync();

            var idsPorNome = userIdsPorNome.Count == 0
                ? new List<int>()
                : await _db.Alunos.AsNoTracking()
                    .Where(a => userIdsPorNome.Contains(a.UsuarioId))
                    .Select(a => a.Id)
                    .Take(5000)
                    .ToListAsync();

            alunoIdsFiltro = idsPorMatricula
                .Concat(idsPorNome)
                .Distinct()
                .ToList();

            if (alunoIdsFiltro.Count == 0)
                return Ok(new PagedResultDto<OfertaAlunoListItemDto>(0, new List<OfertaAlunoListItemDto>()));

            oaBase = oaBase.Where(oa => alunoIdsFiltro.Contains(oa.AlunoId));
        }

        // 3) Total
        var total = await oaBase.CountAsync();

        if (total == 0)
            return Ok(new PagedResultDto<OfertaAlunoListItemDto>(0, new List<OfertaAlunoListItemDto>()));

        // 4) Pegar alunoIds da página, ordenando por Matricula (sem JOIN)
        //    -> buscamos Alunos filtrados pelos alunoIds do oaBase
        var alunoIdsDaOferta = await oaBase
            .Select(oa => oa.AlunoId)
            .ToListAsync();

        // Ordena por matrícula e pagina
        var paginaAlunos = await _db.Alunos.AsNoTracking()
            .Where(a => alunoIdsDaOferta.Contains(a.Id))
            .OrderBy(a => a.Matricula)
            .Skip(skip)
            .Take(limit)
            .Select(a => new { a.Id, a.Matricula, a.UsuarioId })
            .ToListAsync();

        if (paginaAlunos.Count == 0)
            return Ok(new PagedResultDto<OfertaAlunoListItemDto>(total, new List<OfertaAlunoListItemDto>()));

        var paginaAlunoIds = paginaAlunos.Select(a => a.Id).ToList();
        var paginaUsuarioIds = paginaAlunos.Select(a => a.UsuarioId).Distinct().ToList();

        // 5) Mapear Usuario (nome/email/ativo)
        var users = await _db.Usuarios.AsNoTracking()
            .Where(u => paginaUsuarioIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Nome, u.Email, u.Ativo })
            .ToListAsync();

        var userMap = users.ToDictionary(x => x.Id, x => x);

        // 6) Mapear vínculo (Ativo do OfertaAluno) para cada aluno da página
        var vinculos = await _db.OfertaAlunos.AsNoTracking()
            .Where(oa => oa.OfertaDisciplinaId == ofertaId && paginaAlunoIds.Contains(oa.AlunoId))
            .Select(oa => new { oa.AlunoId, oa.Ativo })
            .ToListAsync();

        var vincMap = vinculos
            .GroupBy(x => x.AlunoId)
            .ToDictionary(g => g.Key, g => g.First().Ativo);

        // 7) Montar DTO final
        var items = paginaAlunos.Select(a =>
        {
            userMap.TryGetValue(a.UsuarioId, out var u);
            var ativoUser = u?.Ativo ?? false;
            var ativoVinculo = vincMap.TryGetValue(a.Id, out var av) ? av : false;

            return new OfertaAlunoListItemDto(
                a.Id,
                a.Matricula,
                u?.Nome ?? "",
                u?.Email ?? "",
                ativoUser,
                ativoVinculo
            );
        }).ToList();

        return Ok(new PagedResultDto<OfertaAlunoListItemDto>(total, items));
    }

    // ADMIN: desmatricular (delete lógico)
    [Authorize(Roles = "ADMIN")]
    [HttpDelete("{ofertaId:int}/alunos/{alunoId:int}")]
    public async Task<IActionResult> DesvincularAluno(int ofertaId, int alunoId)
    {
        var vinculo = await _db.OfertaAlunos
            .FirstOrDefaultAsync(x => x.OfertaDisciplinaId == ofertaId && x.AlunoId == alunoId && x.Ativo);

        if (vinculo is null) return NotFound("Vínculo não encontrado.");

        vinculo.Ativo = false;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Roles = "ALUNO,PROFESSOR")]
    [HttpGet("me")]
    public async Task<ActionResult<List<OfertaMeDto>>> GetMe()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = User.FindFirstValue(ClaimTypes.Role);

        if (!int.TryParse(userIdStr, out var userId) || string.IsNullOrWhiteSpace(role))
            return Unauthorized();

        if (role == "PROFESSOR")
        {
            var ofertas = await _db.OfertaDisciplinas
                .AsNoTracking()
                .Where(o => o.Ativa && o.Professor.UsuarioId == userId)
                .Include(o => o.Disciplina)
                .Include(o => o.Professor).ThenInclude(p => p.Usuario)
                .Include(o => o.Turma)
                .OrderByDescending(o => o.Ano).ThenByDescending(o => o.Semestre).ThenBy(o => o.Disciplina.Codigo)
                .Select(o => new OfertaMeDto(
                    o.Id,
                    o.Ano,
                    o.Semestre,
                    o.Periodo.ToString(),
                    o.DisciplinaId,
                    o.Disciplina.Codigo,
                    o.Disciplina.Nome,
                    o.ProfessorId,
                    o.Professor.Matricula,
                    o.Professor.Usuario.Nome,
                    o.TurmaId,
                    o.Turma != null ? o.Turma.Nome : null
                ))
                .ToListAsync();

            return Ok(ofertas);
        }

        // role == "ALUNO"
        var ofertasAluno = await _db.OfertaAlunos
            .AsNoTracking()
            .Where(oa => oa.Ativo
                && oa.Aluno.UsuarioId == userId
                && oa.OfertaDisciplina.Ativa)
            .Select(oa => oa.OfertaDisciplina)
            .Distinct()
            .OrderByDescending(o => o.Ano)
            .ThenByDescending(o => o.Semestre)
            .ThenBy(o => o.Disciplina.Codigo)
            .Select(o => new OfertaMeDto(
                o.Id,
                o.Ano,
                o.Semestre,
                o.Periodo.ToString(),
                o.DisciplinaId,
                o.Disciplina.Codigo,
                o.Disciplina.Nome,
                o.ProfessorId,
                o.Professor.Matricula,
                o.Professor.Usuario.Nome,
                o.TurmaId,
                o.Turma != null ? o.Turma.Nome : null
            ))
            .ToListAsync();

        return Ok(ofertasAluno);

    }

}

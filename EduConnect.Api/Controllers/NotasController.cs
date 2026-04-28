using System.Security.Claims;
using EduConnect.Api.Data;
using EduConnect.Api.Domain;
using EduConnect.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduConnect.Api.Controllers;

[ApiController]
[Route("api/ofertas/{ofertaId:int}/notas")]
[Authorize(Roles = "PROFESSOR")]
public class NotasController : ControllerBase
{
    private readonly AppDbContext _db;
    public NotasController(AppDbContext db) => _db = db;

    private int GetUserId()
    {
        var v = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(v, out var id)) throw new UnauthorizedAccessException();
        return id;
    }

    private sealed record NotaLinha(int OfertaDisciplinaId, decimal? A1, decimal? A2, decimal? A3);


    // GET: tabela de alunos matriculados + notas atuais
    [HttpGet]
    public async Task<ActionResult<List<OfertaNotaRowDto>>> Listar(int ofertaId)
    {
        var userId = GetUserId();

        var profId = await _db.Professores.AsNoTracking()
            .Where(p => p.UsuarioId == userId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        if (profId == 0) return Forbid();

        var ofertaOk = await _db.OfertaDisciplinas.AsNoTracking()
            .AnyAsync(o => o.Id == ofertaId && o.ProfessorId == profId && o.Ativa);

        if (!ofertaOk) return Forbid();

        // alunos da oferta
        var alunos = await (
            from oa in _db.OfertaAlunos.AsNoTracking()
            join a in _db.Alunos.AsNoTracking() on oa.AlunoId equals a.Id
            join u in _db.Usuarios.AsNoTracking() on a.UsuarioId equals u.Id
            where oa.OfertaDisciplinaId == ofertaId && oa.Ativo && u.Ativo
            select new { a.Id, a.Matricula, Nome = u.Nome }
        ).OrderBy(x => x.Matricula).ToListAsync();

        if (alunos.Count == 0) return Ok(new List<OfertaNotaRowDto>());

        var alunoIds = alunos.Select(x => x.Id).ToList();

        var notas = await _db.OfertaNotas.AsNoTracking()
            .Where(n => n.OfertaDisciplinaId == ofertaId && alunoIds.Contains(n.AlunoId))
            .Select(n => new { n.AlunoId, n.A1, n.A2, n.A3, n.AtualizadoEm })
            .ToListAsync();

        var map = notas.ToDictionary(x => x.AlunoId, x => (x.A1, x.A2, x.A3, (DateTime?)x.AtualizadoEm));

        var result = alunos.Select(a =>
        {
            var n = map.TryGetValue(a.Id, out var v) ? v : ((decimal?)null, (decimal?)null, (decimal?)null, (DateTime?)null);
            return new OfertaNotaRowDto(a.Id, a.Matricula, a.Nome, n.Item1, n.Item2, n.Item3, n.Item4);
        }).ToList();

        return Ok(result);
    }

    // PUT: lança/atualiza notas de 1 aluno
    [HttpPut("{alunoId:int}")]
    public async Task<IActionResult> Upsert(int ofertaId, int alunoId, [FromBody] OfertaNotaUpsertDto dto)
    {
        var userId = GetUserId();

        var profId = await _db.Professores.AsNoTracking()
            .Where(p => p.UsuarioId == userId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        if (profId == 0) return Forbid();

        var ofertaOk = await _db.OfertaDisciplinas.AsNoTracking()
            .AnyAsync(o => o.Id == ofertaId && o.ProfessorId == profId && o.Ativa);

        if (!ofertaOk) return Forbid();

        // aluno precisa estar matriculado na oferta
        var matriculado = await _db.OfertaAlunos.AsNoTracking()
            .AnyAsync(oa => oa.OfertaDisciplinaId == ofertaId && oa.AlunoId == alunoId && oa.Ativo);

        if (!matriculado) return NotFound("Aluno não está matriculado na oferta.");

        // valida notas (0..10) quando fornecidas
        bool invalida(decimal? x) => x.HasValue && (x.Value < 0m || x.Value > 10m);
        if (invalida(dto.A1) || invalida(dto.A2) || invalida(dto.A3))
            return BadRequest("Notas devem estar entre 0 e 10.");

        var row = await _db.OfertaNotas
            .FirstOrDefaultAsync(n => n.OfertaDisciplinaId == ofertaId && n.AlunoId == alunoId);

        if (row is null)
        {
            row = new OfertaNota
            {
                OfertaDisciplinaId = ofertaId,
                AlunoId = alunoId
            };
            _db.OfertaNotas.Add(row);
        }

        row.A1 = dto.A1;
        row.A2 = dto.A2;
        row.A3 = dto.A3;
        row.AtualizadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("/api/notas/professor/medias")]
    public async Task<ActionResult<ProfessorMediaProvasDto>> MediasProfessor()
    {
        var userId = GetUserId();

        var profId = await _db.Professores.AsNoTracking()
            .Where(p => p.UsuarioId == userId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        if (profId == 0) return Forbid();

        var ofertaIds = await _db.OfertaDisciplinas.AsNoTracking()
            .Where(o => o.Ativa && o.ProfessorId == profId)
            .Select(o => o.Id)
            .ToListAsync();

        if (ofertaIds.Count == 0)
        {
            return Ok(new ProfessorMediaProvasDto(
                TotalOfertas: 0,
                TotalAlunosComNota: 0,
                MediaA1: null, MediaA2: null, MediaA3: null, MediaGeral: null
            ));
        }

        var notas = await _db.OfertaNotas.AsNoTracking()
            .Where(n => ofertaIds.Contains(n.OfertaDisciplinaId))
            .Select(n => new { n.A1, n.A2, n.A3 })
            .ToListAsync();

        // conta alunos com alguma nota preenchida
        var totalAlunosComNota = notas.Count(n => n.A1.HasValue || n.A2.HasValue || n.A3.HasValue);

        decimal? avg(IEnumerable<decimal?> xs)
        {
            var v = xs.Where(x => x.HasValue).Select(x => x!.Value).ToList();
            if (v.Count == 0) return null;
            return Math.Round(v.Average(), 2, MidpointRounding.AwayFromZero);
        }

        var mediaA1 = avg(notas.Select(x => x.A1));
        var mediaA2 = avg(notas.Select(x => x.A2));
        var mediaA3 = avg(notas.Select(x => x.A3));

        // média geral de todas as provas lançadas (A1/A2/A3 juntos)
        var todas = notas.SelectMany(x => new[] { x.A1, x.A2, x.A3 });
        var mediaGeral = avg(todas);

        return Ok(new ProfessorMediaProvasDto(
            TotalOfertas: ofertaIds.Count,
            TotalAlunosComNota: totalAlunosComNota,
            MediaA1: mediaA1,
            MediaA2: mediaA2,
            MediaA3: mediaA3,
            MediaGeral: mediaGeral
        ));
    }

    [HttpGet("/api/notas/professor/medias/ofertas")]
    public async Task<ActionResult<List<ProfessorMediaProvasOfertaDto>>> MediasPorOferta()
    {
        var userId = GetUserId();

        var profId = await _db.Professores.AsNoTracking()
            .Where(p => p.UsuarioId == userId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        if (profId == 0) return Forbid();

        var ofertas = await (
            from o in _db.OfertaDisciplinas.AsNoTracking()
            join d in _db.Disciplinas.AsNoTracking() on o.DisciplinaId equals d.Id
            where o.Ativa && o.ProfessorId == profId
            select new
            {
                o.Id,
                d.Codigo,
                d.Nome,
                o.Ano,
                o.Semestre,
                Periodo = o.Periodo.ToString()
            }
        ).ToListAsync();

        if (ofertas.Count == 0) return Ok(new List<ProfessorMediaProvasOfertaDto>());

        var ofertaIds = ofertas.Select(x => x.Id).ToList();

        var notas = await _db.OfertaNotas.AsNoTracking()
            .Where(n => ofertaIds.Contains(n.OfertaDisciplinaId))
            .Select(n => new NotaLinha(n.OfertaDisciplinaId, n.A1, n.A2, n.A3))
            .ToListAsync();

        decimal? Avg(IEnumerable<decimal?> xs)
        {
            var v = xs.Where(x => x.HasValue).Select(x => x!.Value).ToList();
            if (v.Count == 0) return null;
            return Math.Round(v.Average(), 2, MidpointRounding.AwayFromZero);
        }

        var group = notas
            .GroupBy(x => x.OfertaDisciplinaId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<ProfessorMediaProvasOfertaDto>();

        foreach (var o in ofertas
                     .OrderByDescending(x => x.Ano)
                     .ThenByDescending(x => x.Semestre)
                     .ThenBy(x => x.Codigo))
        {
            var list = group.TryGetValue(o.Id, out var l) ? l : new List<NotaLinha>();

            var totalAlunosComNota = list.Count(x => x.A1.HasValue || x.A2.HasValue || x.A3.HasValue);

            var mediaA1 = Avg(list.Select(x => x.A1));
            var mediaA2 = Avg(list.Select(x => x.A2));
            var mediaA3 = Avg(list.Select(x => x.A3));
            var mediaGeral = Avg(list.SelectMany(x => new decimal?[] { x.A1, x.A2, x.A3 }));

            result.Add(new ProfessorMediaProvasOfertaDto(
                o.Id, o.Codigo, o.Nome, o.Ano, o.Semestre, o.Periodo,
                totalAlunosComNota,
                mediaA1, mediaA2, mediaA3, mediaGeral
            ));
        }

        return Ok(result);
    }

    [HttpGet("{alunoId:int}")]
    public async Task<ActionResult<OfertaNotaAlunoDto>> GetAluno(int ofertaId, int alunoId)
    {
        var userId = GetUserId();

        var profId = await _db.Professores.AsNoTracking()
            .Where(p => p.UsuarioId == userId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        if (profId == 0) return Forbid();

        var ofertaOk = await _db.OfertaDisciplinas.AsNoTracking()
            .AnyAsync(o => o.Id == ofertaId && o.ProfessorId == profId && o.Ativa);

        if (!ofertaOk) return Forbid();

        var alunoInfo = await (
            from oa in _db.OfertaAlunos.AsNoTracking()
            join a in _db.Alunos.AsNoTracking() on oa.AlunoId equals a.Id
            join u in _db.Usuarios.AsNoTracking() on a.UsuarioId equals u.Id
            where oa.OfertaDisciplinaId == ofertaId && oa.AlunoId == alunoId && oa.Ativo
            select new { a.Id, a.Matricula, Nome = u.Nome }
        ).FirstOrDefaultAsync();

        if (alunoInfo is null)
            return NotFound("Aluno não está matriculado nesta oferta.");

        var nota = await _db.OfertaNotas.AsNoTracking()
            .Where(n => n.OfertaDisciplinaId == ofertaId && n.AlunoId == alunoId)
            .Select(n => new { n.A1, n.A2, n.A3, AtualizadoEm = (DateTime?)n.AtualizadoEm })
            .FirstOrDefaultAsync();

        return Ok(new OfertaNotaAlunoDto(
            ofertaId,
            alunoInfo.Id,
            alunoInfo.Matricula,
            alunoInfo.Nome,
            nota?.A1,
            nota?.A2,
            nota?.A3,
            nota?.AtualizadoEm
        ));
    }

}
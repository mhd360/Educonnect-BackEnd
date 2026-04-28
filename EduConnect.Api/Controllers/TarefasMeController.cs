using EduConnect.Api.Data;
using EduConnect.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EduConnect.Api.Controllers;

[Authorize(Roles = "ALUNO")]
[ApiController]
[Route("api/tarefas/me")]
public class TarefasMeController : ControllerBase
{
    private readonly AppDbContext _db;
    public TarefasMeController(AppDbContext db) => _db = db;

    [HttpGet("pendentes")]
    public async Task<ActionResult<List<TarefaPendenteDto>>> Pendentes([FromQuery] int limit = 10)
    {
        if (limit <= 0) limit = 10;
        if (limit > 50) limit = 50;

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        var alunoId = await _db.Alunos.AsNoTracking()
            .Where(a => a.UsuarioId == userId)
            .Select(a => a.Id)
            .FirstOrDefaultAsync();

        if (alunoId == 0) return Unauthorized();

        // Ofertas do aluno (sem join/navegação)
        var ofertaIds = await _db.OfertaAlunos.AsNoTracking()
            .Where(oa => oa.Ativo && oa.AlunoId == alunoId)
            .Select(oa => oa.OfertaDisciplinaId)
            .Distinct()
            .ToListAsync();

        if (ofertaIds.Count == 0) return Ok(new List<TarefaPendenteDto>());

        // 1) Tarefas pendentes SEM navegar para OfertaDisciplina/Disciplina
        var tarefasPendentes = await _db.Tarefas.AsNoTracking()
            .Where(t => t.Ativa && ofertaIds.Contains(t.OfertaDisciplinaId))
            .Where(t => !_db.TarefaRespostas.Any(r => r.Ativa && r.AlunoId == alunoId && r.TarefaId == t.Id))
            .Select(t => new
            {
                t.OfertaDisciplinaId,
                t.Id,
                t.Titulo,
                t.DataEntrega,
                t.Peso
            })
            .ToListAsync();

        if (tarefasPendentes.Count == 0) return Ok(new List<TarefaPendenteDto>());

        // 2) Busca infos de oferta/disciplina em outra query
        var ofertaIdsDasTarefas = tarefasPendentes.Select(x => x.OfertaDisciplinaId).Distinct().ToList();

        var ofertasInfo = await _db.OfertaDisciplinas.AsNoTracking()
            .Where(o => o.Ativa && ofertaIdsDasTarefas.Contains(o.Id))
            .Select(o => new
            {
                o.Id,
                Codigo = o.Disciplina.Codigo,
                Nome = o.Disciplina.Nome
            })
            .ToListAsync();

        var dictOferta = ofertasInfo.ToDictionary(x => x.Id, x => (x.Codigo, x.Nome));

        // 3) Monta DTO final (descarta tarefas de ofertas inativas)
        var list = tarefasPendentes
            .Where(t => dictOferta.ContainsKey(t.OfertaDisciplinaId))
            .Select(t =>
            {
                var (codigo, nome) = dictOferta[t.OfertaDisciplinaId];
                return new TarefaPendenteDto(
                    t.OfertaDisciplinaId,
                    codigo,
                    nome,
                    t.Id,
                    t.Titulo,
                    t.DataEntrega,
                    t.Peso
                );
            })
            .OrderBy(x => x.DataEntrega == null)
            .ThenBy(x => x.DataEntrega)
            .ThenBy(x => x.DisciplinaCodigo)
            .ThenBy(x => x.Titulo)
            .Take(limit)
            .ToList();

        return Ok(list);
    }

    [HttpGet("respondidas")]
    public async Task<ActionResult<List<TarefaRespondidaDto>>> Respondidas([FromQuery] int limit = 10)
    {
        if (limit <= 0) limit = 10;
        if (limit > 50) limit = 50;

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        var alunoId = await _db.Alunos.AsNoTracking()
            .Where(a => a.UsuarioId == userId)
            .Select(a => a.Id)
            .FirstOrDefaultAsync();

        if (alunoId == 0) return Unauthorized();

        // 1) Respostas do aluno que ainda NÃO têm correção ativa
        var raw = await
            (from r in _db.TarefaRespostas.AsNoTracking()
             where r.Ativa && r.AlunoId == alunoId

             join t in _db.Tarefas.AsNoTracking()
                on r.TarefaId equals t.Id
             where t.Ativa

             join o in _db.OfertaDisciplinas.AsNoTracking()
                on t.OfertaDisciplinaId equals o.Id
             where o.Ativa

             where !_db.TarefaCorrecoes.AsNoTracking()
                .Any(c => c.Ativa && c.TarefaRespostaId == r.Id)

             select new
             {
                 OfertaId = o.Id,
                 DisciplinaId = o.DisciplinaId,
                 TarefaId = t.Id,
                 TarefaTitulo = t.Titulo,
                 TarefaRespostaId = r.Id,
                 r.DataEnvio
             })
            .ToListAsync();

        if (raw.Count == 0) return Ok(new List<TarefaRespondidaDto>());

        // 2) Busca disciplinas em outra query (mesmo padrão do seu "corrigidas")
        var discIds = raw.Select(x => x.DisciplinaId).Distinct().ToList();

        var discMap = await _db.Disciplinas.AsNoTracking()
            .Where(d => discIds.Contains(d.Id))
            .Select(d => new { d.Id, d.Codigo, d.Nome })
            .ToDictionaryAsync(d => d.Id, d => (d.Codigo, d.Nome));

        var list = raw
            .Select(x =>
            {
                var disc = discMap.TryGetValue(x.DisciplinaId, out var d) ? d : ("", "");
                return new TarefaRespondidaDto(
                    x.OfertaId,
                    disc.Item1,
                    disc.Item2,
                    x.TarefaId,
                    x.TarefaTitulo,
                    x.TarefaRespostaId,
                    x.DataEnvio
                );
            })
            .OrderByDescending(x => x.DataEnvio)
            .ThenBy(x => x.DisciplinaCodigo)
            .ThenBy(x => x.TarefaTitulo)
            .Take(limit)
            .ToList();

        return Ok(list);
    }


    [HttpGet("corrigidas")]
    public async Task<ActionResult<List<TarefaCorrigidaDto>>> Corrigidas([FromQuery] int limit = 10)
    {
        if (limit <= 0) limit = 10;
        if (limit > 50) limit = 50;

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        var alunoId = await _db.Alunos.AsNoTracking()
            .Where(a => a.UsuarioId == userId)
            .Select(a => a.Id)
            .FirstOrDefaultAsync();

        if (alunoId == 0) return Unauthorized();

        // 1) Busca correções do aluno (sem navegar para Disciplina no SQL)
        var raw = await
            (from r in _db.TarefaRespostas.AsNoTracking()
             where r.Ativa && r.AlunoId == alunoId
             join c in _db.TarefaCorrecoes.AsNoTracking()
                on r.Id equals c.TarefaRespostaId
             where c.Ativa
             join t in _db.Tarefas.AsNoTracking()
                on r.TarefaId equals t.Id
             where t.Ativa
             join o in _db.OfertaDisciplinas.AsNoTracking()
                on t.OfertaDisciplinaId equals o.Id
             where o.Ativa
             select new
             {
                 OfertaId = o.Id,
                 DisciplinaId = o.DisciplinaId,
                 TarefaId = t.Id,
                 TarefaTitulo = t.Titulo,
                 c.Nota,
                 c.Feedback,
                 c.DataCorrecao
             })
            .ToListAsync();

        if (raw.Count == 0) return Ok(new List<TarefaCorrigidaDto>());

        // 2) Busca disciplinas em outra query (evita joins instáveis)
        var discIds = raw.Select(x => x.DisciplinaId).Distinct().ToList();
        var discMap = await _db.Disciplinas.AsNoTracking()
            .Where(d => discIds.Contains(d.Id))
            .Select(d => new { d.Id, d.Codigo, d.Nome })
            .ToDictionaryAsync(d => d.Id, d => (d.Codigo, d.Nome));

        var list = raw
            .Select(x =>
            {
                var disc = discMap.TryGetValue(x.DisciplinaId, out var d) ? d : ("", "");
                return new TarefaCorrigidaDto(
                    x.OfertaId,
                    disc.Item1,
                    disc.Item2,
                    x.TarefaId,
                    x.TarefaTitulo,
                    x.Nota,
                    x.Feedback,
                    x.DataCorrecao
                );
            })
            .OrderByDescending(x => x.DataCorrecao)
            .ThenBy(x => x.DisciplinaCodigo)
            .ThenBy(x => x.TarefaTitulo)
            .Take(limit)
            .ToList();

        return Ok(list);
    }
}

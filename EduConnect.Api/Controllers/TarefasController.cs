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
[Route("api/ofertas/{ofertaId:int}/tarefas")]
public class TarefasController : ControllerBase
{
    private readonly AppDbContext _db;
    public TarefasController(AppDbContext db) => _db = db;

    // PROFESSOR: cria tarefa na própria oferta
    [Authorize(Roles = "PROFESSOR")]
    [HttpPost]
    public async Task<ActionResult<TarefaReadDto>> Create(int ofertaId, TarefaCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Titulo)) return BadRequest("Título é obrigatório.");
        if (dto.Peso <= 0) return BadRequest("Peso inválido.");

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        var ofertaOk = await _db.OfertaDisciplinas
            .AsNoTracking()
            .AnyAsync(o => o.Id == ofertaId && o.Ativa && o.Professor.UsuarioId == userId);

        if (!ofertaOk) return Forbid();

        var tarefa = new Tarefa
        {
            OfertaDisciplinaId = ofertaId,
            Titulo = dto.Titulo.Trim(),
            Descricao = string.IsNullOrWhiteSpace(dto.Descricao) ? null : dto.Descricao.Trim(),
            DataEntrega = dto.DataEntrega,
            Peso = dto.Peso,
            Ativa = true
        };

        _db.Tarefas.Add(tarefa);
        await _db.SaveChangesAsync();

        return Ok(new TarefaReadDto(
            tarefa.Id,
            tarefa.OfertaDisciplinaId,
            tarefa.Titulo,
            tarefa.Descricao,
            tarefa.DataEntrega,
            tarefa.Peso,
            tarefa.Ativa
        ));
    }

    // ALUNO/PROFESSOR: lista tarefas da oferta (com validação de acesso)
    [Authorize(Roles = "ALUNO,PROFESSOR,ADMIN")]
    [HttpGet]
    public async Task<ActionResult<List<TarefaReadDto>>> GetAll(int ofertaId, [FromQuery] bool includeInativas = false)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId) || string.IsNullOrWhiteSpace(role))
            return Unauthorized();

        if (role == "PROFESSOR")
        {
            var ok = await _db.OfertaDisciplinas
                .AsNoTracking()
                .AnyAsync(o => o.Id == ofertaId && o.Professor.UsuarioId == userId);

            if (!ok) return Forbid();
        }
        else if (role == "ALUNO")
        {
            var ok = await _db.OfertaAlunos
                .AsNoTracking()
                .AnyAsync(oa => oa.OfertaDisciplinaId == ofertaId && oa.Ativo && oa.Aluno.UsuarioId == userId);

            if (!ok) return Forbid();
        }
        // ADMIN: não valida vínculo

        var query = _db.Tarefas
            .AsNoTracking()
            .Where(a => a.OfertaDisciplinaId == ofertaId);

        if (!includeInativas)
            query = query.Where(a => a.Ativa);

        var list = await query
            .OrderBy(a => a.DataEntrega ?? DateTime.MaxValue)
            .ThenBy(a => a.Titulo)
            .Select(a => new TarefaReadDto(
                a.Id,
                a.OfertaDisciplinaId,
                a.Titulo,
                a.Descricao,
                a.DataEntrega,
                a.Peso,
                a.Ativa
            ))
            .ToListAsync();

        return Ok(list);
    }

    [Authorize(Roles = "PROFESSOR")]
    [HttpDelete("{tarefaId:int}")]
    public async Task<IActionResult> Delete(int ofertaId, int tarefaId)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        // oferta precisa ser do professor
        var ofertaOk = await _db.OfertaDisciplinas
            .AsNoTracking()
            .AnyAsync(o => o.Id == ofertaId && o.Ativa && o.Professor.UsuarioId == userId);

        if (!ofertaOk) return Forbid();

        // tarefa precisa existir e pertencer à oferta
        var tarefa = await _db.Tarefas
            .FirstOrDefaultAsync(t => t.Id == tarefaId && t.OfertaDisciplinaId == ofertaId);

        if (tarefa is null) return NotFound("Tarefa não encontrada.");

        // delete lógico
        if (!tarefa.Ativa) return NoContent();

        tarefa.Ativa = false;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("corrigidas")]
    public async Task<ActionResult<List<TarefaCorrigidaDto>>> Corrigidas([FromQuery] int limit = 50)
    {
        if (limit <= 0) limit = 50;
        if (limit > 200) limit = 200;

        var userIdStr = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        var profId = await _db.Professores.AsNoTracking()
            .Where(p => p.UsuarioId == userId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        if (profId == 0) return Forbid();

        // 1) Ofertas do professor
        var ofertaIds = await _db.OfertaDisciplinas.AsNoTracking()
            .Where(o => o.Ativa && o.ProfessorId == profId)
            .Select(o => o.Id)
            .ToListAsync();

        if (ofertaIds.Count == 0)
            return Ok(new List<TarefaCorrigidaDto>());

        // 2) Tarefas das ofertas
        var tarefas = await _db.Tarefas.AsNoTracking()
            .Where(t => t.Ativa && ofertaIds.Contains(t.OfertaDisciplinaId))
            .Select(t => new { t.Id, t.OfertaDisciplinaId, t.Titulo })
            .ToListAsync();

        if (tarefas.Count == 0)
            return Ok(new List<TarefaCorrigidaDto>());

        var tarefaIds = tarefas.Select(t => t.Id).ToList();
        var tarefaMap = tarefas.ToDictionary(t => t.Id, t => t);

        // 3) Respostas (para ligar correção -> tarefa)
        var respostas = await _db.TarefaRespostas.AsNoTracking()
            .Where(r => r.Ativa && tarefaIds.Contains(r.TarefaId))
            .Select(r => new { r.Id, r.TarefaId })
            .ToListAsync();

        if (respostas.Count == 0)
            return Ok(new List<TarefaCorrigidaDto>());

        var respostaMap = respostas.ToDictionary(r => r.Id, r => r);
        var respostaIds = respostas.Select(r => r.Id).ToList();

        // 4) Correções mais recentes (limit)
        var correcoes = await _db.TarefaCorrecoes.AsNoTracking()
            .Where(c => c.Ativa && respostaIds.Contains(c.TarefaRespostaId))
            .OrderByDescending(c => c.DataCorrecao)
            .Take(limit)
            .Select(c => new { c.TarefaRespostaId, c.Nota, c.Feedback, c.DataCorrecao })
            .ToListAsync();

        if (correcoes.Count == 0)
            return Ok(new List<TarefaCorrigidaDto>());

        // 5) Mapa Oferta -> Disciplina
        var discMap = await (
            from o in _db.OfertaDisciplinas.AsNoTracking()
            join d in _db.Disciplinas.AsNoTracking() on o.DisciplinaId equals d.Id
            where ofertaIds.Contains(o.Id)
            select new { o.Id, d.Codigo, d.Nome }
        ).ToDictionaryAsync(x => x.Id, x => (x.Codigo, x.Nome));

        // 6) Montagem do DTO (usando seu TarefaCorrigidaDto)
        var result = new List<TarefaCorrigidaDto>(correcoes.Count);

        foreach (var c in correcoes)
        {
            if (!respostaMap.TryGetValue(c.TarefaRespostaId, out var r))
                continue;

            if (!tarefaMap.TryGetValue(r.TarefaId, out var t))
                continue;

            var disc = discMap.TryGetValue(t.OfertaDisciplinaId, out var dd) ? dd : ("", "");

            result.Add(new TarefaCorrigidaDto(
                OfertaId: t.OfertaDisciplinaId,
                DisciplinaCodigo: disc.Item1,
                DisciplinaNome: disc.Item2,
                TarefaId: t.Id,
                TarefaTitulo: t.Titulo,
                Nota: c.Nota,
                Feedback: c.Feedback,
                DataCorrecao: c.DataCorrecao
            ));
        }

        return Ok(result);
    }
}

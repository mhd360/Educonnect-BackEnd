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
[Route("api/ofertas/{ofertaId:int}/eventos")]
public class EventosController : ControllerBase
{
    private readonly AppDbContext _db;
    public EventosController(AppDbContext db) => _db = db;

    // PROFESSOR: criar evento na própria oferta
    [Authorize(Roles = "PROFESSOR")]
    [HttpPost]
    public async Task<ActionResult<EventoReadDto>> Create(int ofertaId, [FromBody] EventoCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Titulo)) return BadRequest("Título é obrigatório.");

        if (dto.DiaInteiro)
        {
            if (dto.HoraInicio is not null || dto.HoraFim is not null)
                return BadRequest("Evento dia inteiro não deve ter HoraInicio/HoraFim.");
        }
        else
        {
            if (dto.HoraInicio is null || dto.HoraFim is null)
                return BadRequest("HoraInicio e HoraFim são obrigatórias quando não é dia inteiro.");

            if (dto.HoraFim <= dto.HoraInicio)
                return BadRequest("HoraFim deve ser maior que HoraInicio.");
        }

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        var professorId = await _db.Professores.AsNoTracking()
            .Where(p => p.UsuarioId == userId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        if (professorId == 0) return Unauthorized();

        var ofertaOk = await _db.OfertaDisciplinas.AsNoTracking()
            .AnyAsync(o => o.Id == ofertaId && o.Ativa && o.ProfessorId == professorId);

        if (!ofertaOk) return Forbid();

        var ev = new Evento
        {
            OfertaDisciplinaId = ofertaId,
            Titulo = dto.Titulo.Trim(),
            Descricao = string.IsNullOrWhiteSpace(dto.Descricao) ? null : dto.Descricao.Trim(),
            Data = dto.Data,
            DiaInteiro = dto.DiaInteiro,
            HoraInicio = dto.DiaInteiro ? null : dto.HoraInicio,
            HoraFim = dto.DiaInteiro ? null : dto.HoraFim,
            Ativo = true
        };

        _db.Eventos.Add(ev);
        await _db.SaveChangesAsync();

        return Ok(new EventoReadDto(ev.Id, ev.OfertaDisciplinaId, ev.Titulo, ev.Descricao, ev.Data, ev.DiaInteiro, ev.HoraInicio, ev.HoraFim, ev.Ativo));
    }

    // ALUNO/PROFESSOR: listar eventos da oferta (com validação)
    // ADMIN não tem acesso -> não está no Authorize Roles
    [Authorize(Roles = "ALUNO,PROFESSOR")]
    [HttpGet]
    public async Task<ActionResult<List<EventoReadDto>>> GetAll(int ofertaId, [FromQuery] bool includeInativos = false)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId) || string.IsNullOrWhiteSpace(role))
            return Unauthorized();

        if (role == "PROFESSOR")
        {
            var professorId = await _db.Professores.AsNoTracking()
                .Where(p => p.UsuarioId == userId)
                .Select(p => p.Id)
                .FirstOrDefaultAsync();

            if (professorId == 0) return Unauthorized();

            var ok = await _db.OfertaDisciplinas.AsNoTracking()
                .AnyAsync(o => o.Id == ofertaId && o.ProfessorId == professorId);

            if (!ok) return Forbid();
        }
        else // ALUNO
        {
            var alunoId = await _db.Alunos.AsNoTracking()
                .Where(a => a.UsuarioId == userId)
                .Select(a => a.Id)
                .FirstOrDefaultAsync();

            if (alunoId == 0) return Unauthorized();

            var ok = await _db.OfertaAlunos.AsNoTracking()
                .AnyAsync(oa => oa.OfertaDisciplinaId == ofertaId && oa.AlunoId == alunoId && oa.Ativo);

            if (!ok) return Forbid();
        }

        var query = _db.Eventos.AsNoTracking()
            .Where(e => e.OfertaDisciplinaId == ofertaId);

        if (!includeInativos)
            query = query.Where(e => e.Ativo);

        var list = await query
            .OrderBy(e => e.Data)
            .ThenBy(e => e.HoraInicio ?? new TimeOnly(0, 0))
            .Select(e => new EventoReadDto(
                e.Id, e.OfertaDisciplinaId, e.Titulo, e.Descricao,
                e.Data, e.DiaInteiro, e.HoraInicio, e.HoraFim, e.Ativo
            ))
            .ToListAsync();

        return Ok(list);
    }

    // PROFESSOR: editar evento (somente na própria oferta)
    [Authorize(Roles = "PROFESSOR")]
    [HttpPut("{eventoId:int}")]
    public async Task<IActionResult> Update(int ofertaId, int eventoId, EventoUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Titulo)) return BadRequest("Título é obrigatório.");

        if (dto.DiaInteiro)
        {
            if (dto.HoraInicio is not null || dto.HoraFim is not null)
                return BadRequest("Evento dia inteiro não deve ter HoraInicio/HoraFim.");
        }
        else
        {
            if (dto.HoraInicio is null || dto.HoraFim is null)
                return BadRequest("HoraInicio e HoraFim são obrigatórias quando não é dia inteiro.");

            if (dto.HoraFim <= dto.HoraInicio)
                return BadRequest("HoraFim deve ser maior que HoraInicio.");
        }

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        var professorId = await _db.Professores.AsNoTracking()
            .Where(p => p.UsuarioId == userId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        if (professorId == 0) return Unauthorized();

        var ofertaOk = await _db.OfertaDisciplinas.AsNoTracking()
            .AnyAsync(o => o.Id == ofertaId && o.ProfessorId == professorId);

        if (!ofertaOk) return Forbid();

        var ev = await _db.Eventos.FirstOrDefaultAsync(e => e.Id == eventoId && e.OfertaDisciplinaId == ofertaId);
        if (ev is null) return NotFound();

        ev.Titulo = dto.Titulo.Trim();
        ev.Descricao = string.IsNullOrWhiteSpace(dto.Descricao) ? null : dto.Descricao.Trim();
        ev.Data = dto.Data;
        ev.DiaInteiro = dto.DiaInteiro;
        ev.HoraInicio = dto.DiaInteiro ? null : dto.HoraInicio;
        ev.HoraFim = dto.DiaInteiro ? null : dto.HoraFim;
        ev.Ativo = dto.Ativo;
        ev.Ativo = dto.Ativo;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // PROFESSOR: desativar evento
    [Authorize(Roles = "PROFESSOR")]
    [HttpDelete("{eventoId:int}")]
    public async Task<IActionResult> Delete(int ofertaId, int eventoId)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        var professorId = await _db.Professores.AsNoTracking()
            .Where(p => p.UsuarioId == userId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        if (professorId == 0) return Unauthorized();

        var ofertaOk = await _db.OfertaDisciplinas.AsNoTracking()
            .AnyAsync(o => o.Id == ofertaId && o.ProfessorId == professorId);

        if (!ofertaOk) return Forbid();

        var ev = await _db.Eventos.FirstOrDefaultAsync(e => e.Id == eventoId && e.OfertaDisciplinaId == ofertaId);
        if (ev is null) return NotFound();

        ev.Ativo = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // PROFESSOR: listar todos os eventos de todas as ofertas que ele leciona
    [Authorize(Roles = "PROFESSOR")]
    [HttpGet("/api/eventos/professor/me")]
    public async Task<ActionResult<List<EventoProfessorReadDto>>> GetAllMinhasDisciplinas(
        [FromQuery] bool includeInativos = false)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        var professorId = await _db.Professores.AsNoTracking()
            .Where(p => p.UsuarioId == userId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        if (professorId == 0) return Unauthorized();

        // 1) ofertas do professor
        var ofertas = await _db.OfertaDisciplinas.AsNoTracking()
            .Where(o => o.ProfessorId == professorId && o.Ativa)
            .Select(o => new { o.Id, o.DisciplinaId })
            .ToListAsync();

        if (ofertas.Count == 0)
            return Ok(new List<EventoProfessorReadDto>());

        var ofertaIds = ofertas.Select(x => x.Id).ToList();
        var disciplinaIds = ofertas.Select(x => x.DisciplinaId).Distinct().ToList();

        // 2) disciplinas (código/nome)
        var disciplinas = await _db.Disciplinas.AsNoTracking()
            .Where(d => disciplinaIds.Contains(d.Id))
            .Select(d => new { d.Id, d.Codigo, d.Nome })
            .ToListAsync();

        var discMap = disciplinas.ToDictionary(x => x.Id, x => (x.Codigo, x.Nome));
        var ofertaDiscMap = ofertas.ToDictionary(x => x.Id, x => x.DisciplinaId);

        // 3) eventos de todas as ofertas
        var query = _db.Eventos.AsNoTracking()
            .Where(e => ofertaIds.Contains(e.OfertaDisciplinaId));

        if (!includeInativos)
            query = query.Where(e => e.Ativo);

        var eventos = await query
            .OrderBy(e => e.Data)
            .ThenBy(e => e.HoraInicio ?? new TimeOnly(0, 0))
            .ToListAsync();

        // 4) monta DTO final
        var result = new List<EventoProfessorReadDto>(eventos.Count);

        foreach (var e in eventos)
        {
            var discId = ofertaDiscMap.TryGetValue(e.OfertaDisciplinaId, out var did) ? did : 0;
            var disc = discMap.TryGetValue(discId, out var d) ? d : ("", "");

            result.Add(new EventoProfessorReadDto(
                EventoId: e.Id,
                OfertaId: e.OfertaDisciplinaId,
                DisciplinaCodigo: disc.Item1,
                DisciplinaNome: disc.Item2,
                Titulo: e.Titulo,
                Descricao: e.Descricao,
                Data: e.Data,
                DiaInteiro: e.DiaInteiro,
                HoraInicio: e.HoraInicio,
                HoraFim: e.HoraFim,
                Ativo: e.Ativo
            ));
        }

        return Ok(result);
    }
}

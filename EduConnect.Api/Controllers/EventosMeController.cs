using EduConnect.Api.Data;
using EduConnect.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EduConnect.Api.Controllers;

[Authorize(Roles = "ALUNO,PROFESSOR")]
[ApiController]
[Route("api/eventos/me")]
public class EventosMeController : ControllerBase
{
    private readonly AppDbContext _db;
    public EventosMeController(AppDbContext db) => _db = db;

    [HttpGet("proximos")]
    public async Task<ActionResult<List<ProximoEventoDto>>> Proximos([FromQuery] int limit = 3)
    {
        if (limit <= 0) limit = 3;
        if (limit > 20) limit = 20;

        var role = User.FindFirstValue(ClaimTypes.Role);
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId) || string.IsNullOrWhiteSpace(role))
            return Unauthorized();

        // referência "agora" (sem timezone): usa a data/hora local do servidor
        // como evento está em DateOnly/TimeOnly, isso é suficiente para ordenar.
        var hoje = DateOnly.FromDateTime(DateTime.Now);
        var agoraHora = TimeOnly.FromDateTime(DateTime.Now);

        if (role == "PROFESSOR")
        {
            var professorId = await _db.Professores.AsNoTracking()
                .Where(p => p.UsuarioId == userId)
                .Select(p => p.Id)
                .FirstOrDefaultAsync();

            if (professorId == 0) return Unauthorized();

            var query = _db.Eventos.AsNoTracking()
                .Where(e => e.Ativo && e.OfertaDisciplina.Ativa && e.OfertaDisciplina.ProfessorId == professorId);

            // futuros (data maior) ou hoje com hora >= agora (dia inteiro conta como 00:00)
            query = query.Where(e =>
                e.Data > hoje ||
                (e.Data == hoje && (e.DiaInteiro || (e.HoraInicio.HasValue && e.HoraInicio.Value >= agoraHora)))
            );

            var list = await query
                .OrderBy(e => e.Data)
                .ThenBy(e => e.DiaInteiro ? new TimeOnly(0, 0) : (e.HoraInicio ?? new TimeOnly(0, 0)))
                .Take(limit)
                .Select(e => new ProximoEventoDto(
                    e.Id,
                    e.OfertaDisciplinaId,
                    e.OfertaDisciplina.Disciplina.Codigo,
                    e.OfertaDisciplina.Disciplina.Nome,
                    e.Titulo,
                    e.Data,
                    e.DiaInteiro,
                    e.HoraInicio,
                    e.HoraFim
                ))
                .ToListAsync();

            return Ok(list);
        }

        // role == ALUNO
        var alunoId = await _db.Alunos.AsNoTracking()
            .Where(a => a.UsuarioId == userId)
            .Select(a => a.Id)
            .FirstOrDefaultAsync();

        if (alunoId == 0) return Unauthorized();

        var ofertaIds = _db.OfertaAlunos.AsNoTracking()
            .Where(oa => oa.Ativo && oa.AlunoId == alunoId && oa.OfertaDisciplina.Ativa)
            .Select(oa => oa.OfertaDisciplinaId);

        var queryAluno = _db.Eventos.AsNoTracking()
            .Where(e => e.Ativo && ofertaIds.Contains(e.OfertaDisciplinaId));

        queryAluno = queryAluno.Where(e =>
            e.Data > hoje ||
            (e.Data == hoje && (e.DiaInteiro || (e.HoraInicio.HasValue && e.HoraInicio.Value >= agoraHora)))
        );

        var listAluno = await queryAluno
            .OrderBy(e => e.Data)
            .ThenBy(e => e.DiaInteiro ? new TimeOnly(0, 0) : (e.HoraInicio ?? new TimeOnly(0, 0)))
            .Take(limit)
            .Select(e => new ProximoEventoDto(
                e.Id,
                e.OfertaDisciplinaId,
                e.OfertaDisciplina.Disciplina.Codigo,
                e.OfertaDisciplina.Disciplina.Nome,
                e.Titulo,
                e.Data,
                e.DiaInteiro,
                e.HoraInicio,
                e.HoraFim
            ))
            .ToListAsync();

        return Ok(listAluno);
    }
}

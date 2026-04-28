using EduConnect.Api.Data;
using EduConnect.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EduConnect.Api.Controllers;

[Authorize(Roles = "PROFESSOR")]
[ApiController]
[Route("api/tarefas/me")]
public class TarefasProfessorMeController : ControllerBase
{
    private readonly AppDbContext _db;
    public TarefasProfessorMeController(AppDbContext db) => _db = db;

    [HttpGet("para-corrigir")]
    public async Task<ActionResult<List<TarefaParaCorrigirDto>>> ParaCorrigir([FromQuery] int limit = 10)
    {
        if (limit <= 0) limit = 10;
        if (limit > 50) limit = 50;

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        var professorId = await _db.Professores.AsNoTracking()
            .Where(p => p.UsuarioId == userId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        if (professorId == 0) return Unauthorized();

        // Query principal sem "Include": só projeção (mais estável)
        var listRaw = await
            (from o in _db.OfertaDisciplinas.AsNoTracking()
             where o.Ativa && o.ProfessorId == professorId
             from t in _db.Tarefas.AsNoTracking()
             where t.Ativa && t.OfertaDisciplinaId == o.Id
             join r in _db.TarefaRespostas.AsNoTracking() on t.Id equals r.TarefaId
             where r.Ativa
             join a in _db.Alunos.AsNoTracking() on r.AlunoId equals a.Id
             join u in _db.Usuarios.AsNoTracking() on a.UsuarioId equals u.Id
             // não pode ter correção ativa para essa resposta
             where !_db.TarefaCorrecoes.Any(c => c.Ativa && c.TarefaRespostaId == r.Id)
             select new
             {
                 OfertaId = o.Id,
                 DisciplinaId = o.DisciplinaId,
                 TarefaId = t.Id,
                 TarefaTitulo = t.Titulo,
                 t.DataEntrega,
                 AlunoId = a.Id,
                 AlunoMatricula = a.Matricula,
                 AlunoNome = u.Nome,
                 RespostaId = r.Id,
                 r.DataEnvio
             })
            .ToListAsync();

        if (listRaw.Count == 0) return Ok(new List<TarefaParaCorrigirDto>());

        // Busca disciplina (2ª query) para evitar problemas de join/navegação
        var discIds = listRaw.Select(x => x.DisciplinaId).Distinct().ToList();
        var discMap = await _db.Disciplinas.AsNoTracking()
            .Where(d => discIds.Contains(d.Id))
            .Select(d => new { d.Id, d.Codigo, d.Nome })
            .ToDictionaryAsync(d => d.Id, d => (d.Codigo, d.Nome));

        var list = listRaw
            .Select(x =>
            {
                var disc = discMap.TryGetValue(x.DisciplinaId, out var d) ? d : ("", "");
                return new TarefaParaCorrigirDto(
                    x.OfertaId,
                    disc.Item1,
                    disc.Item2,
                    x.TarefaId,
                    x.TarefaTitulo,
                    x.DataEntrega,
                    x.AlunoId,
                    x.AlunoMatricula,
                    x.AlunoNome,
                    x.RespostaId,
                    x.DataEnvio
                );
            })
            .OrderBy(x => x.DataEntrega == null)
            .ThenBy(x => x.DataEntrega)
            .ThenBy(x => x.DataEnvio)
            .ThenBy(x => x.AlunoMatricula)
            .Take(limit)
            .ToList();

        return Ok(list);
    }
}

using System.Security.Claims;
using EduConnect.Api.Data;
using EduConnect.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduConnect.Api.Controllers;

[ApiController]
[Route("api/notas/me")]
[Authorize(Roles = "ALUNO")]
public class MinhasNotasController : ControllerBase
{
    private readonly AppDbContext _db;
    public MinhasNotasController(AppDbContext db) => _db = db;

    private int GetUserId()
    {
        var v = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(v, out var id)) throw new UnauthorizedAccessException();
        return id;
    }

    [HttpGet]
    public async Task<ActionResult<List<MinhasNotasItemDto>>> Get()
    {
        var userId = GetUserId();

        var aluno = await _db.Alunos.AsNoTracking()
            .Where(a => a.UsuarioId == userId)
            .Select(a => new { a.Id })
            .FirstOrDefaultAsync();

        if (aluno is null) return Unauthorized();

        // minhas ofertas ativas + disciplina + notas A1/A2/A3
        var result = await (
            from oa in _db.OfertaAlunos.AsNoTracking()
            join o in _db.OfertaDisciplinas.AsNoTracking() on oa.OfertaDisciplinaId equals o.Id
            join d in _db.Disciplinas.AsNoTracking() on o.DisciplinaId equals d.Id
            join n in _db.OfertaNotas.AsNoTracking()
                on new { OfertaId = o.Id, AlunoId = aluno.Id }
                equals new { OfertaId = n.OfertaDisciplinaId, n.AlunoId }
                into ng
            from n in ng.DefaultIfEmpty()
            where oa.AlunoId == aluno.Id && oa.Ativo && o.Ativa
            orderby o.Ano descending, o.Semestre descending, d.Codigo
            select new MinhasNotasItemDto(
                o.Id,
                d.Codigo,
                d.Nome,
                o.Ano,
                o.Semestre,
                o.Periodo.ToString(),
                n != null ? n.A1 : null,
                n != null ? n.A2 : null,
                n != null ? n.A3 : null
            )
        ).ToListAsync();

        return Ok(result);
    }
}
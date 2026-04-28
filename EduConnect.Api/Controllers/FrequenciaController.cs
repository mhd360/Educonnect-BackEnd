using EduConnect.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EduConnect.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/ofertas/{ofertaId:int}/frequencia")]
public class FrequenciaController : ControllerBase
{
    private readonly AppDbContext _db;
    public FrequenciaController(AppDbContext db) => _db = db;

    private async Task<int?> GetProfessorId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return null;

        var profId = await _db.Professores.AsNoTracking()
            .Where(p => p.UsuarioId == userId)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync();

        return profId;
    }

    // PROFESSOR: alterar total de aulas da oferta
    [Authorize(Roles = "PROFESSOR")]
    [HttpPut("total-aulas")]
    public async Task<IActionResult> AtualizarTotalAulas(int ofertaId, [FromBody] TotalAulasDto dto)
    {
        if (dto.TotalAulas < 1 || dto.TotalAulas > 300)
            return BadRequest("TotalAulas inválido.");

        var profId = await GetProfessorId();
        if (profId is null) return Unauthorized();

        var oferta = await _db.OfertaDisciplinas
            .FirstOrDefaultAsync(o => o.Id == ofertaId && o.ProfessorId == profId.Value);

        if (oferta is null) return Forbid();

        oferta.TotalAulas = dto.TotalAulas;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // PROFESSOR: marcar falta (idempotente)
    [Authorize(Roles = "PROFESSOR")]
    [HttpPost("faltas")]
    public async Task<IActionResult> MarcarFalta(int ofertaId, [FromBody] MarcarFaltaDto dto)
    {
        var profId = await GetProfessorId();
        if (profId is null) return Unauthorized();

        var oferta = await _db.OfertaDisciplinas.AsNoTracking()
            .Where(o => o.Id == ofertaId && o.ProfessorId == profId.Value)
            .Select(o => new { o.Id, o.TotalAulas })
            .FirstOrDefaultAsync();

        if (oferta is null) return Forbid();

        if (dto.NumeroAula < 1 || dto.NumeroAula > oferta.TotalAulas)
            return BadRequest("NumeroAula fora do intervalo do TotalAulas.");

        var matriculado = await _db.OfertaAlunos.AsNoTracking()
            .AnyAsync(oa => oa.OfertaDisciplinaId == ofertaId && oa.AlunoId == dto.AlunoId && oa.Ativo);

        if (!matriculado) return NotFound("Aluno não está matriculado na oferta.");

        var existente = await _db.FaltaOfertaAlunos
            .FirstOrDefaultAsync(x =>
                x.OfertaDisciplinaId == ofertaId &&
                x.AlunoId == dto.AlunoId &&
                x.NumeroAula == dto.NumeroAula);

        if (existente is null)
        {
            _db.FaltaOfertaAlunos.Add(new EduConnect.Api.Domain.FaltaOfertaAluno
            {
                OfertaDisciplinaId = ofertaId,
                AlunoId = dto.AlunoId,
                NumeroAula = dto.NumeroAula,
                Ativa = true
            });
        }
        else
        {
            existente.Ativa = true; // reativa se estava desativada
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // PROFESSOR: desmarcar falta
    [Authorize(Roles = "PROFESSOR")]
    [HttpDelete("faltas")]
    public async Task<IActionResult> DesmarcarFalta(int ofertaId, [FromBody] MarcarFaltaDto dto)
    {
        var profId = await GetProfessorId();
        if (profId is null) return Unauthorized();

        var ofertaOk = await _db.OfertaDisciplinas.AsNoTracking()
            .AnyAsync(o => o.Id == ofertaId && o.ProfessorId == profId.Value);

        if (!ofertaOk) return Forbid();

        var existente = await _db.FaltaOfertaAlunos
            .FirstOrDefaultAsync(x =>
                x.OfertaDisciplinaId == ofertaId &&
                x.AlunoId == dto.AlunoId &&
                x.NumeroAula == dto.NumeroAula);

        if (existente is null) return NoContent();

        existente.Ativa = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ALUNO: ver sua frequência (presenças/faltas/%)
    [Authorize(Roles = "ALUNO")]
    [HttpGet("me")]
    public async Task<ActionResult<FrequenciaReadDto>> GetMe(int ofertaId)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        var aluno = await _db.Alunos.AsNoTracking()
            .Where(a => a.UsuarioId == userId)
            .Select(a => new { a.Id })
            .FirstOrDefaultAsync();

        if (aluno is null) return Unauthorized();

        var matriculado = await _db.OfertaAlunos.AsNoTracking()
            .AnyAsync(oa => oa.OfertaDisciplinaId == ofertaId && oa.AlunoId == aluno.Id && oa.Ativo);

        if (!matriculado) return Forbid();

        return await MontarFrequencia(ofertaId, aluno.Id);
    }

    // PROFESSOR/ADMIN: ver frequência de um aluno
    [Authorize(Roles = "PROFESSOR,ADMIN")]
    [HttpGet("{alunoId:int}")]
    public async Task<ActionResult<FrequenciaReadDto>> GetAluno(int ofertaId, int alunoId)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);

        if (role == "PROFESSOR")
        {
            var profId = await GetProfessorId();
            if (profId is null) return Unauthorized();

            var ofertaOk = await _db.OfertaDisciplinas.AsNoTracking()
                .AnyAsync(o => o.Id == ofertaId && o.ProfessorId == profId.Value);

            if (!ofertaOk) return Forbid();
        }

        var matriculado = await _db.OfertaAlunos.AsNoTracking()
            .AnyAsync(oa => oa.OfertaDisciplinaId == ofertaId && oa.AlunoId == alunoId && oa.Ativo);

        if (!matriculado) return NotFound("Aluno não está matriculado na oferta.");

        return await MontarFrequencia(ofertaId, alunoId);
    }

    private async Task<ActionResult<FrequenciaReadDto>> MontarFrequencia(int ofertaId, int alunoId)
    {
        var totalAulas = await _db.OfertaDisciplinas.AsNoTracking()
            .Where(o => o.Id == ofertaId)
            .Select(o => o.TotalAulas)
            .FirstOrDefaultAsync();

        if (totalAulas <= 0) totalAulas = 16;

        var faltas = await _db.FaltaOfertaAlunos.AsNoTracking()
            .Where(f => f.OfertaDisciplinaId == ofertaId && f.AlunoId == alunoId && f.Ativa)
            .Select(f => f.NumeroAula)
            .Distinct()
            .ToListAsync();

        var faltasCount = faltas.Count;
        var presencas = Math.Max(0, totalAulas - faltasCount);
        var percentual = totalAulas == 0 ? 0m : Math.Round((decimal)presencas * 100m / totalAulas, 2);

        return Ok(new FrequenciaReadDto(totalAulas, faltasCount, presencas, percentual, faltas.OrderBy(x => x).ToList()));
    }

    // PROFESSOR/ADMIN: consultar total de aulas da oferta
    [Authorize(Roles = "PROFESSOR,ADMIN")]
    [HttpGet("total-aulas")]
    public async Task<ActionResult<OfertaTotalAulasReadDto>> GetTotalAulas(int ofertaId)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);

        if (role == "PROFESSOR")
        {
            var profId = await GetProfessorId();
            if (profId is null) return Unauthorized();

            var ofertaOk = await _db.OfertaDisciplinas.AsNoTracking()
                .AnyAsync(o => o.Id == ofertaId && o.ProfessorId == profId.Value);

            if (!ofertaOk) return Forbid();
        }

        var totalAulas = await _db.OfertaDisciplinas.AsNoTracking()
            .Where(o => o.Id == ofertaId)
            .Select(o => o.TotalAulas)
            .FirstOrDefaultAsync();

        if (totalAulas <= 0) totalAulas = 16;

        return Ok(new OfertaTotalAulasReadDto(ofertaId, totalAulas));
    }


    // PROFESSOR: consultar quais alunos faltaram em qual aula (por oferta)
    [Authorize(Roles = "PROFESSOR")]
    [HttpGet("faltas")]
    public async Task<ActionResult<List<FaltaAulaReadDto>>> GetFaltasPorAula(
        int ofertaId,
        [FromQuery] bool includeInativos = false)
    {
        var profId = await GetProfessorId();
        if (profId is null) return Unauthorized();

        var oferta = await _db.OfertaDisciplinas.AsNoTracking()
            .Where(o => o.Id == ofertaId && o.ProfessorId == profId.Value)
            .Select(o => new { o.Id, o.TotalAulas })
            .FirstOrDefaultAsync();

        if (oferta is null) return Forbid();

        // faltas ativas na oferta
        var faltasRaw = await _db.FaltaOfertaAlunos.AsNoTracking()
            .Where(f => f.OfertaDisciplinaId == ofertaId && f.Ativa)
            .Select(f => new { f.AlunoId, f.NumeroAula })
            .Distinct()
            .ToListAsync();

        if (faltasRaw.Count == 0)
            return Ok(new List<FaltaAulaReadDto>());

        var alunoIds = faltasRaw.Select(x => x.AlunoId).Distinct().ToList();

        // alunos (matricula + usuarioId)
        var alunos = await _db.Alunos.AsNoTracking()
            .Where(a => alunoIds.Contains(a.Id))
            .Select(a => new { a.Id, a.Matricula, a.UsuarioId })
            .ToListAsync();

        var usuarioIds = alunos.Select(a => a.UsuarioId).Distinct().ToList();

        var usuarios = await _db.Usuarios.AsNoTracking()
            .Where(u => usuarioIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Nome, u.Ativo })
            .ToListAsync();

        var userMap = usuarios.ToDictionary(x => x.Id, x => x);
        var alunoMap = alunos.ToDictionary(
            x => x.Id,
            x =>
            {
                var u = userMap.TryGetValue(x.UsuarioId, out var uu) ? uu : null;
                return new { x.Id, x.Matricula, Nome = u?.Nome ?? "", UsuarioAtivo = u?.Ativo ?? false };
            });

        // se não incluir inativos, remove usuários inativos
        IEnumerable<(int NumeroAula, FaltaAulaAlunoDto Aluno)> flat = faltasRaw
            .Where(x => alunoMap.ContainsKey(x.AlunoId))
            .Select(x =>
            {
                var a = alunoMap[x.AlunoId];
                return (x.NumeroAula, new FaltaAulaAlunoDto(a.Id, a.Matricula, a.Nome), a.UsuarioAtivo);
            })
            .Where(x => includeInativos || x.UsuarioAtivo)
            .Select(x => (x.NumeroAula, x.Item2));

        var result = flat
            .GroupBy(x => x.NumeroAula)
            .OrderBy(g => g.Key)
            .Select(g => new FaltaAulaReadDto(
                g.Key,
                g.Select(x => x.Aluno)
                 .OrderBy(a => a.Matricula)
                 .ToList()
            ))
            .ToList();

        return Ok(result);
    }
}

public record TotalAulasDto(int TotalAulas);
public record MarcarFaltaDto(int AlunoId, int NumeroAula);
public record FrequenciaReadDto(int TotalAulas, int Faltas, int Presencas, decimal Percentual, List<int> AulasComFalta);
public record OfertaTotalAulasReadDto(int OfertaId, int TotalAulas);
public record FaltaAulaAlunoDto(int AlunoId, string Matricula, string Nome);
public record FaltaAulaReadDto(int NumeroAula, List<FaltaAulaAlunoDto> Alunos);
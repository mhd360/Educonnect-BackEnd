using EduConnect.Api.Data;
using EduConnect.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EduConnect.Api.Controllers;

[Authorize(Roles = "ALUNO,PROFESSOR")]
[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;
    public DashboardController(AppDbContext db) => _db = db;

    [HttpGet("me")]
    public async Task<ActionResult<DashboardMeDto>> GetMe()
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdStr, out var userId) || string.IsNullOrWhiteSpace(role))
            return Unauthorized();

        var nome = await _db.Usuarios.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Nome)
            .FirstOrDefaultAsync();

        if (nome is null) return Unauthorized();

        // Próximos eventos (reaproveita a lógica via query, sem chamar controller)
        var proximosEventos = await BuscarProximosEventos(role, userId, limit: 3);

        if (role == "ALUNO")
        {
            var pendentes = await BuscarTarefasPendentesAluno(userId, limit: 10);
            var corrigidas = await BuscarTarefasCorrigidasAluno(userId, limit: 10);

            var matricula = await _db.Alunos.AsNoTracking()
                .Where(a => a.UsuarioId == userId)
                .Select(a => a.Matricula)
                .FirstOrDefaultAsync();

            return Ok(new DashboardMeDto(
                "ALUNO",
                userId,
                nome,
                matricula,
                proximosEventos,
                pendentes,
                corrigidas,
                null
            ));
        }

        // PROFESSOR
        var paraCorrigir = await BuscarTarefasParaCorrigirProfessor(userId, limit: 10);

        var matriculaProf = await _db.Professores.AsNoTracking()
            .Where(p => p.UsuarioId == userId)
            .Select(p => p.Matricula)
            .FirstOrDefaultAsync();

        return Ok(new DashboardMeDto(
            "PROFESSOR",
            userId,
            nome,
            matriculaProf,
            proximosEventos,
            null,
            null,
            paraCorrigir
        ));
    }

    // ===== helpers =====

    private async Task<List<ProximoEventoDto>> BuscarProximosEventos(string role, int userId, int limit)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Now);
        var agoraHora = TimeOnly.FromDateTime(DateTime.Now);

        if (role == "PROFESSOR")
        {
            var professorId = await _db.Professores.AsNoTracking()
                .Where(p => p.UsuarioId == userId)
                .Select(p => p.Id)
                .FirstOrDefaultAsync();

            if (professorId == 0) return new List<ProximoEventoDto>();

            var query = _db.Eventos.AsNoTracking()
                .Where(e => e.Ativo && e.OfertaDisciplina.Ativa && e.OfertaDisciplina.ProfessorId == professorId)
                .Where(e =>
                    e.Data > hoje ||
                    (e.Data == hoje && (e.DiaInteiro || (e.HoraInicio.HasValue && e.HoraInicio.Value >= agoraHora)))
                );

            return await query
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
        }

        // ALUNO
        var alunoId = await _db.Alunos.AsNoTracking()
            .Where(a => a.UsuarioId == userId)
            .Select(a => a.Id)
            .FirstOrDefaultAsync();

        if (alunoId == 0) return new List<ProximoEventoDto>();

        var ofertaIds = _db.OfertaAlunos.AsNoTracking()
            .Where(oa => oa.Ativo && oa.AlunoId == alunoId && oa.OfertaDisciplina.Ativa)
            .Select(oa => oa.OfertaDisciplinaId);

        var queryAluno = _db.Eventos.AsNoTracking()
            .Where(e => e.Ativo && ofertaIds.Contains(e.OfertaDisciplinaId))
            .Where(e =>
                e.Data > hoje ||
                (e.Data == hoje && (e.DiaInteiro || (e.HoraInicio.HasValue && e.HoraInicio.Value >= agoraHora)))
            );

        return await queryAluno
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
    }

    // Usa o MESMO método estável que você já tem em TarefasMeController (2 queries)
    private async Task<List<TarefaPendenteDto>> BuscarTarefasPendentesAluno(int userId, int limit)
    {
        var alunoId = await _db.Alunos.AsNoTracking()
            .Where(a => a.UsuarioId == userId)
            .Select(a => a.Id)
            .FirstOrDefaultAsync();

        if (alunoId == 0) return new List<TarefaPendenteDto>();

        var ofertaIds = await _db.OfertaAlunos.AsNoTracking()
            .Where(oa => oa.Ativo && oa.AlunoId == alunoId)
            .Select(oa => oa.OfertaDisciplinaId)
            .Distinct()
            .ToListAsync();

        if (ofertaIds.Count == 0) return new List<TarefaPendenteDto>();

        var tarefasPendentes = await _db.Tarefas.AsNoTracking()
            .Where(t => t.Ativa && ofertaIds.Contains(t.OfertaDisciplinaId))
            .Where(t => !_db.TarefaRespostas.Any(r => r.Ativa && r.AlunoId == alunoId && r.TarefaId == t.Id))
            .Select(t => new { t.OfertaDisciplinaId, t.Id, t.Titulo, t.DataEntrega, t.Peso })
            .ToListAsync();

        if (tarefasPendentes.Count == 0) return new List<TarefaPendenteDto>();

        var ofertaIdsDasTarefas = tarefasPendentes.Select(x => x.OfertaDisciplinaId).Distinct().ToList();

        var ofertasInfo = await _db.OfertaDisciplinas.AsNoTracking()
            .Where(o => o.Ativa && ofertaIdsDasTarefas.Contains(o.Id))
            .Select(o => new { o.Id, Codigo = o.Disciplina.Codigo, Nome = o.Disciplina.Nome })
            .ToListAsync();

        var dictOferta = ofertasInfo.ToDictionary(x => x.Id, x => (x.Codigo, x.Nome));

        return tarefasPendentes
            .Where(t => dictOferta.ContainsKey(t.OfertaDisciplinaId))
            .Select(t =>
            {
                var (codigo, nome) = dictOferta[t.OfertaDisciplinaId];
                return new TarefaPendenteDto(t.OfertaDisciplinaId, codigo, nome, t.Id, t.Titulo, t.DataEntrega, t.Peso);
            })
            .OrderBy(x => x.DataEntrega == null)
            .ThenBy(x => x.DataEntrega)
            .ThenBy(x => x.DisciplinaCodigo)
            .ThenBy(x => x.Titulo)
            .Take(limit)
            .ToList();
    }

    private async Task<List<TarefaCorrigidaDto>> BuscarTarefasCorrigidasAluno(int userId, int limit)
    {
        var alunoId = await _db.Alunos.AsNoTracking()
            .Where(a => a.UsuarioId == userId)
            .Select(a => a.Id)
            .FirstOrDefaultAsync();

        if (alunoId == 0) return new List<TarefaCorrigidaDto>();

        var raw = await
            (from r in _db.TarefaRespostas.AsNoTracking()
             where r.Ativa && r.AlunoId == alunoId
             join c in _db.TarefaCorrecoes.AsNoTracking() on r.Id equals c.TarefaRespostaId
             where c.Ativa
             join t in _db.Tarefas.AsNoTracking() on r.TarefaId equals t.Id
             where t.Ativa
             join o in _db.OfertaDisciplinas.AsNoTracking() on t.OfertaDisciplinaId equals o.Id
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
                 }
            )
            .ToListAsync();

        if (raw.Count == 0) return new List<TarefaCorrigidaDto>();

        var discIds = raw.Select(x => x.DisciplinaId).Distinct().ToList();
        var discMap = await _db.Disciplinas.AsNoTracking()
            .Where(d => discIds.Contains(d.Id))
            .Select(d => new { d.Id, d.Codigo, d.Nome })
            .ToDictionaryAsync(d => d.Id, d => (d.Codigo, d.Nome));

        return raw
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
    }

    private async Task<List<TarefaParaCorrigirDto>> BuscarTarefasParaCorrigirProfessor(int userId, int limit)
    {
        var professorId = await _db.Professores.AsNoTracking()
            .Where(p => p.UsuarioId == userId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        if (professorId == 0) return new List<TarefaParaCorrigirDto>();

        var listRaw = await
            (from o in _db.OfertaDisciplinas.AsNoTracking()
             where o.Ativa && o.ProfessorId == professorId
             from t in _db.Tarefas.AsNoTracking()
             where t.Ativa && t.OfertaDisciplinaId == o.Id
             join r in _db.TarefaRespostas.AsNoTracking() on t.Id equals r.TarefaId
             where r.Ativa
             join a in _db.Alunos.AsNoTracking() on r.AlunoId equals a.Id
             join u in _db.Usuarios.AsNoTracking() on a.UsuarioId equals u.Id
             where !_db.TarefaCorrecoes.Any(c => c.Ativa && c.TarefaRespostaId == r.Id)
             select new { OfertaId = o.Id, o.DisciplinaId, TarefaId = t.Id, t.Titulo, t.DataEntrega, AlunoId = a.Id, a.Matricula, Nome = u.Nome, RespostaId = r.Id, r.DataEnvio })
            .ToListAsync();

        if (listRaw.Count == 0) return new List<TarefaParaCorrigirDto>();

        var discIds = listRaw.Select(x => x.DisciplinaId).Distinct().ToList();
        var discMap = await _db.Disciplinas.AsNoTracking()
            .Where(d => discIds.Contains(d.Id))
            .Select(d => new { d.Id, d.Codigo, d.Nome })
            .ToDictionaryAsync(d => d.Id, d => (d.Codigo, d.Nome));

        return listRaw
            .Select(x =>
            {
                var disc = discMap.TryGetValue(x.DisciplinaId, out var d) ? d : ("", "");
                return new TarefaParaCorrigirDto(
                    x.OfertaId, disc.Item1, disc.Item2,
                    x.TarefaId, x.Titulo, x.DataEntrega,
                    x.AlunoId, x.Matricula, x.Nome,
                    x.RespostaId, x.DataEnvio
                );
            })
            .OrderBy(x => x.DataEntrega == null)
            .ThenBy(x => x.DataEntrega)
            .ThenBy(x => x.DataEnvio)
            .ThenBy(x => x.AlunoMatricula)
            .Take(limit)
            .ToList();
    }
}

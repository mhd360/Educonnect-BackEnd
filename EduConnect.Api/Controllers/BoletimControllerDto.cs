using EduConnect.Api.Data;
using EduConnect.Api.Domain;
using EduConnect.Api.DTOs;
using EduConnect.Api.Pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using System.Security.Claims;

namespace EduConnect.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BoletimController : ControllerBase
{
    private readonly AppDbContext _db;

    public BoletimController(AppDbContext db)
    {
        _db = db;
    }

    // ========= Helpers =========

    private int GetUserId()
    {
        // 1) Claims (recomendado)
        var claimCandidates = new[]
        {
        ClaimTypes.NameIdentifier, // padrão
        "sub",                     // JWT padrão
        "id",
        "userId",
        "userid",
        "UserId",
        "UsuarioId",
        "usuarioId"
    };

        foreach (var key in claimCandidates)
        {
            var v = User?.FindFirstValue(key);
            if (!string.IsNullOrWhiteSpace(v) && int.TryParse(v, out var idFromClaim))
                return idFromClaim;
        }

        // 2) HttpContext.Items (fallback, caso seu middleware use isso)
        var itemCandidates = new[] { "UserId", "UsuarioId", "usuarioId", "userId", "userid" };

        foreach (var k in itemCandidates)
        {
            if (HttpContext.Items.TryGetValue(k, out var obj))
            {
                if (obj is int idInt) return idInt;
                if (obj is string s && int.TryParse(s, out var idStr)) return idStr;
            }
        }

        // Se chegou aqui, não veio ID no token e nem no middleware
        throw new UnauthorizedAccessException("UserId não encontrado no contexto.");
    }

    private static decimal Round2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    private static (StatusDisciplinaBoletim status, decimal? notaFinal) CalcularStatus(decimal? media, decimal frequenciaPct, decimal? notaExame)
    {
        // frequência < 75% reprova direto
        if (frequenciaPct < 75m)
            return (StatusDisciplinaBoletim.Reprovado, media);

        if (!media.HasValue)
            return (StatusDisciplinaBoletim.EmCurso, null);

        var m = media.Value;

        if (m >= 6m)
            return (StatusDisciplinaBoletim.Aprovado, m);

        if (m < 3m)
            return (StatusDisciplinaBoletim.Reprovado, m);

        // 3 <= média < 6 => Exame
        if (!notaExame.HasValue)
            return (StatusDisciplinaBoletim.Exame, m);

        var nf = (m + notaExame.Value) / 2m;
        return (nf >= 6m ? StatusDisciplinaBoletim.Aprovado : StatusDisciplinaBoletim.Reprovado, nf);
    }

    // ========= Endpoints =========

    // ALUNO: resumo do boletim por ofertas ativas (para listar na tela)
    [HttpGet("me")]
    [Authorize(Roles = "ALUNO")]
    public async Task<ActionResult<List<BoletimOfertaMeResumoDto>>> GetMeuResumo()
    {
        var userId = GetUserId();

        var aluno = await _db.Alunos.AsNoTracking()
            .FirstOrDefaultAsync(a => a.UsuarioId == userId);

        if (aluno is null)
            return NotFound("Aluno não encontrado.");

        // ofertas do aluno (ativas)
        var ofertas = await (
            from oa in _db.OfertaAlunos.AsNoTracking()
            join o in _db.OfertaDisciplinas.AsNoTracking() on oa.OfertaDisciplinaId equals o.Id
            join d in _db.Disciplinas.AsNoTracking() on o.DisciplinaId equals d.Id
            where oa.AlunoId == aluno.Id && oa.Ativo && o.Ativa
            select new
            {
                OfertaId = o.Id,
                DisciplinaCodigo = d.Codigo,
                DisciplinaNome = d.Nome,
                CargaHoraria = d.CargaHoraria,
                o.Ano,
                o.Semestre,
                Periodo = o.Periodo.ToString(),
                TotalAulas = o.TotalAulas,
                NotaExame = oa.NotaExame
            }
        ).ToListAsync();

        if (ofertas.Count == 0)
            return Ok(new List<BoletimOfertaMeResumoDto>());

        var ofertaIds = ofertas.Select(x => x.OfertaId).Distinct().ToList();

        // tarefas ativas por oferta
        var tarefas = await _db.Tarefas.AsNoTracking()
            .Where(t => ofertaIds.Contains(t.OfertaDisciplinaId) && t.Ativa)
            .Select(t => new { t.Id, t.OfertaDisciplinaId, t.Peso })
            .ToListAsync();

        var tarefaIds = tarefas.Select(t => t.Id).ToList();

        // respostas do aluno
        var respostas = await _db.TarefaRespostas.AsNoTracking()
            .Where(r => r.AlunoId == aluno.Id && r.Ativa && tarefaIds.Contains(r.TarefaId))
            .Select(r => new { r.Id, r.TarefaId })
            .ToListAsync();

        var respostaIds = respostas.Select(r => r.Id).ToList();

        // correções do aluno
        var correcoes = await _db.TarefaCorrecoes.AsNoTracking()
            .Where(c => c.Ativa && respostaIds.Contains(c.TarefaRespostaId))
            .Select(c => new { c.TarefaRespostaId, c.Nota })
            .ToListAsync();

        var respToNota = correcoes
            .GroupBy(x => x.TarefaRespostaId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.TarefaRespostaId).First().Nota);

        // faltas do aluno por ofertaAluno
        var ofertaAlunoIds = await _db.OfertaAlunos.AsNoTracking()
            .Where(oa => oa.AlunoId == aluno.Id && oa.Ativo && ofertaIds.Contains(oa.OfertaDisciplinaId))
            .Select(oa => new { oa.Id, oa.OfertaDisciplinaId })
            .ToListAsync();

        var oaIds = ofertaAlunoIds.Select(x => x.Id).ToList();

        var faltasPorOferta = await _db.FaltaOfertaAlunos.AsNoTracking()
            .Where(f => f.Ativa && f.AlunoId == aluno.Id && ofertaIds.Contains(f.OfertaDisciplinaId))
            .GroupBy(f => f.OfertaDisciplinaId)
            .Select(g => new { OfertaId = g.Key, Faltas = g.Count() })
            .ToDictionaryAsync(x => x.OfertaId, x => x.Faltas);


        // calcular por oferta
        var result = new List<BoletimOfertaMeResumoDto>();

        foreach (var of in ofertas)
        {
            var tarefasDaOferta = tarefas.Where(t => t.OfertaDisciplinaId == of.OfertaId).ToList();
            var pesoTotal = tarefasDaOferta.Sum(t => t.Peso);

            decimal pesoCorrigido = 0m;
            decimal soma = 0m;

            foreach (var t in tarefasDaOferta)
            {
                var resp = respostas.FirstOrDefault(r => r.TarefaId == t.Id);
                if (resp is null) continue;

                if (respToNota.TryGetValue(resp.Id, out var nota))
                {
                    pesoCorrigido += t.Peso;
                    soma += nota * t.Peso;
                }
            }

            decimal? media = null;
            if (pesoCorrigido > 0m)
                media = Round2(soma / pesoCorrigido);

            var oaRow = ofertaAlunoIds.First(x => x.OfertaDisciplinaId == of.OfertaId);
            var faltas = faltasPorOferta.TryGetValue(of.OfertaId, out var f) ? f : 0;

            var totalAulas = of.TotalAulas <= 0 ? 16 : of.TotalAulas;
            if (faltas > totalAulas) faltas = totalAulas;

            var presencas = totalAulas - faltas;
            var freqPct = totalAulas == 0 ? 0m : Round2((decimal)presencas * 100m / totalAulas);

            var (status, notaFinal) = CalcularStatus(media, freqPct, of.NotaExame);

            result.Add(new BoletimOfertaMeResumoDto(
                of.OfertaId,
                of.DisciplinaCodigo,
                of.DisciplinaNome,
                of.Ano,
                of.Semestre,
                of.Periodo,
                media,
                freqPct,
                status,
                notaFinal
            ));
        }

        // ordena por data "acadêmica"
        result = result
            .OrderByDescending(x => x.Ano)
            .ThenByDescending(x => x.Semestre)
            .ThenBy(x => x.DisciplinaCodigo)
            .ToList();

        return Ok(result);
    }

    // PROFESSOR: resumo dos alunos da oferta (boletim + frequência)
    [HttpGet("ofertas/{ofertaId:int}/alunos")]
    [Authorize(Roles = "PROFESSOR")]
    public async Task<ActionResult<BoletimOfertaResumoDto>> GetResumoOferta(int ofertaId)
    {
        var userId = GetUserId();

        var prof = await _db.Professores.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UsuarioId == userId);

        if (prof is null)
            return NotFound("Professor não encontrado.");

        var ofertaInfo = await (
            from o in _db.OfertaDisciplinas.AsNoTracking()
            join d in _db.Disciplinas.AsNoTracking() on o.DisciplinaId equals d.Id
            where o.Id == ofertaId && o.ProfessorId == prof.Id
            select new
            {
                OfertaId = o.Id,
                DisciplinaCodigo = d.Codigo,
                DisciplinaNome = d.Nome,
                CargaHoraria = d.CargaHoraria,
                o.Ano,
                o.Semestre,
                Periodo = o.Periodo.ToString(),
                TotalAulas = o.TotalAulas
            }
        ).FirstOrDefaultAsync();

        if (ofertaInfo is null)
            return NotFound("Oferta não encontrada ou não pertence ao professor.");

        // alunos vinculados
        var alunos = await (
            from oa in _db.OfertaAlunos.AsNoTracking()
            join a in _db.Alunos.AsNoTracking() on oa.AlunoId equals a.Id
            join u in _db.Usuarios.AsNoTracking() on a.UsuarioId equals u.Id
            where oa.OfertaDisciplinaId == ofertaId && oa.Ativo
            select new
            {
                OfertaAlunoId = oa.Id,
                oa.AlunoId,
                a.Matricula,
                Nome = u.Nome,
                NotaExame = oa.NotaExame
            }
        ).ToListAsync();

        if (alunos.Count == 0)
        {
            return Ok(new BoletimOfertaResumoDto(
                ofertaInfo.OfertaId,
                ofertaInfo.DisciplinaCodigo,
                ofertaInfo.DisciplinaNome,
                ofertaInfo.CargaHoraria,
                ofertaInfo.Ano,
                ofertaInfo.Semestre,
                ofertaInfo.Periodo,
                0,
                new List<BoletimAlunoResumoDto>()
            ));
        }

        // tarefas da oferta
        var tarefas = await _db.Tarefas.AsNoTracking()
            .Where(t => t.OfertaDisciplinaId == ofertaId && t.Ativa)
            .Select(t => new { t.Id, t.Peso })
            .ToListAsync();

        var tarefaIds = tarefas.Select(x => x.Id).ToList();

        // respostas
        var respostas = await _db.TarefaRespostas.AsNoTracking()
            .Where(r => r.Ativa && tarefaIds.Contains(r.TarefaId) && alunos.Select(a => a.AlunoId).Contains(r.AlunoId))
            .Select(r => new { r.Id, r.TarefaId, r.AlunoId })
            .ToListAsync();

        var respostaIds = respostas.Select(r => r.Id).ToList();

        // correções
        var correcoes = await _db.TarefaCorrecoes.AsNoTracking()
            .Where(c => c.Ativa && respostaIds.Contains(c.TarefaRespostaId))
            .Select(c => new { c.TarefaRespostaId, c.Nota })
            .ToListAsync();

        var notaPorResposta = correcoes
            .GroupBy(x => x.TarefaRespostaId)
            .ToDictionary(g => g.Key, g => g.First().Nota);

        // faltas (por ofertaAlunoId)
        var oaIds = alunos.Select(a => a.OfertaAlunoId).ToList();
        var faltasPorOa = await _db.OfertaAlunoFaltas.AsNoTracking()
            .Where(f => oaIds.Contains(f.OfertaAlunoId))
            .GroupBy(f => f.OfertaAlunoId)
            .Select(g => new { OfertaAlunoId = g.Key, Faltas = g.Count() })
            .ToListAsync();

        var faltasMap = faltasPorOa.ToDictionary(x => x.OfertaAlunoId, x => x.Faltas);

        var totalAulas = ofertaInfo.TotalAulas <= 0 ? 16 : ofertaInfo.TotalAulas;
        var pesoTotal = tarefas.Sum(t => t.Peso);
        var totalTarefas = tarefas.Count;

        var list = new List<BoletimAlunoResumoDto>();

        var faltasPorAluno = await _db.FaltaOfertaAlunos.AsNoTracking()
            .Where(f => f.Ativa && f.OfertaDisciplinaId == ofertaId && alunos.Select(a => a.AlunoId).Contains(f.AlunoId))
            .GroupBy(f => f.AlunoId)
            .Select(g => new { AlunoId = g.Key, Faltas = g.Count() })
            .ToDictionaryAsync(x => x.AlunoId, x => x.Faltas);

        foreach (var a in alunos)
        {
            var faltas = faltasPorAluno.TryGetValue(a.AlunoId, out var f) ? f : 0;
            if (faltas > totalAulas) faltas = totalAulas;

            var presencas = totalAulas - faltas;
            var freqPct = totalAulas == 0 ? 0m : Round2((decimal)presencas * 100m / totalAulas);

            var respostasDoAluno = respostas.Where(r => r.AlunoId == a.AlunoId).ToList();
            var tarefasEnviadas = respostasDoAluno.Select(r => r.TarefaId).Distinct().Count();

            decimal pesoCorrigido = 0m;
            decimal soma = 0m;
            int tarefasCorrigidas = 0;

            foreach (var r in respostasDoAluno)
            {
                var peso = tarefas.FirstOrDefault(t => t.Id == r.TarefaId)?.Peso ?? 0m;
                if (peso <= 0m) continue;

                if (notaPorResposta.TryGetValue(r.Id, out var nota))
                {
                    tarefasCorrigidas++;
                    pesoCorrigido += peso;
                    soma += nota * peso;
                }
            }

            decimal? media = null;
            if (pesoCorrigido > 0m)
                media = Round2(soma / pesoCorrigido);

            var (status, notaFinal) = CalcularStatus(media, freqPct, a.NotaExame);

            list.Add(new BoletimAlunoResumoDto(
                a.AlunoId,
                a.Matricula,
                a.Nome,
                media,
                Round2(pesoCorrigido),
                totalTarefas,
                tarefasEnviadas,
                tarefasCorrigidas,
                totalAulas,
                faltas,
                freqPct,
                status,
                notaFinal
            ));
        }

        list = list
            .OrderBy(x => x.Matricula)
            .ToList();

        return Ok(new BoletimOfertaResumoDto(
            ofertaInfo.OfertaId,
            ofertaInfo.DisciplinaCodigo,
            ofertaInfo.DisciplinaNome,
            ofertaInfo.CargaHoraria,
            ofertaInfo.Ano,
            ofertaInfo.Semestre,
            ofertaInfo.Periodo,
            alunos.Count,
            list
        ));
    }

    // PROFESSOR: detalhar boletim de um aluno na oferta (inclui itens/tarefas + frequência/status)
    [HttpGet("ofertas/{ofertaId:int}/alunos/{alunoId:int}")]
    [Authorize(Roles = "PROFESSOR")]
    public async Task<ActionResult<BoletimOfertaDto>> GetDetalheAluno(int ofertaId, int alunoId)
    {
        var userId = GetUserId();

        var prof = await _db.Professores.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UsuarioId == userId);

        if (prof is null)
            return NotFound("Professor não encontrado.");

        var oferta = await (
            from o in _db.OfertaDisciplinas.AsNoTracking()
            join d in _db.Disciplinas.AsNoTracking() on o.DisciplinaId equals d.Id
            where o.Id == ofertaId && o.ProfessorId == prof.Id
            select new
            {
                o.Id,
                DisciplinaCodigo = d.Codigo,
                DisciplinaNome = d.Nome,
                CargaHoraria = d.CargaHoraria,
                o.Ano,
                o.Semestre,
                o.Periodo,
                TotalAulas = o.TotalAulas
            }
        ).FirstOrDefaultAsync();

        if (oferta is null)
            return NotFound("Oferta não encontrada ou não pertence ao professor.");

        var aluno = await (
            from a in _db.Alunos.AsNoTracking()
            join u in _db.Usuarios.AsNoTracking() on a.UsuarioId equals u.Id
            where a.Id == alunoId
            select new { a.Id, a.Matricula, Nome = u.Nome }
        ).FirstOrDefaultAsync();

        if (aluno is null)
            return NotFound("Aluno não encontrado.");

        var oa = await _db.OfertaAlunos.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OfertaDisciplinaId == ofertaId && x.AlunoId == alunoId && x.Ativo);

        if (oa is null)
            return NotFound("Aluno não está vinculado a esta oferta.");

        var totalAulas = oferta.TotalAulas <= 0 ? 16 : oferta.TotalAulas;

        var faltas = await _db.FaltaOfertaAlunos.AsNoTracking()
            .CountAsync(f => f.Ativa && f.OfertaDisciplinaId == ofertaId && f.AlunoId == alunoId);

        if (faltas > totalAulas) faltas = totalAulas;

        var presencas = totalAulas - faltas;
        var freqPct = totalAulas == 0 ? 0m : Round2((decimal)presencas * 100m / totalAulas);

        var tarefas = await _db.Tarefas.AsNoTracking()
            .Where(t => t.OfertaDisciplinaId == ofertaId && t.Ativa)
            .Select(t => new { t.Id, t.Titulo, t.DataEntrega, t.Peso })
            .ToListAsync();

        var tarefaIds = tarefas.Select(t => t.Id).ToList();

        var respostas = await _db.TarefaRespostas.AsNoTracking()
            .Where(r => r.Ativa && r.AlunoId == alunoId && tarefaIds.Contains(r.TarefaId))
            .Select(r => new { r.Id, r.TarefaId, r.DataEnvio })
            .ToListAsync();

        var respostaIds = respostas.Select(r => r.Id).ToList();

        var correcoes = await _db.TarefaCorrecoes.AsNoTracking()
            .Where(c => c.Ativa && respostaIds.Contains(c.TarefaRespostaId))
            .Select(c => new { c.TarefaRespostaId, c.Nota, c.Feedback, c.DataCorrecao })
            .ToListAsync();

        var correcaoMap = correcoes
            .GroupBy(x => x.TarefaRespostaId)
            .ToDictionary(g => g.Key, g => g.First());

        var itens = new List<BoletimItemDto>();

        decimal pesoTotal = tarefas.Sum(t => t.Peso);
        decimal pesoCorrigido = 0m;
        decimal soma = 0m;

        foreach (var t in tarefas.OrderBy(x => x.DataEntrega).ThenBy(x => x.Titulo))
        {
            var resp = respostas.FirstOrDefault(r => r.TarefaId == t.Id);

            var enviada = resp is not null;
            var dataEnvio = resp?.DataEnvio;

            var corrigida = false;
            decimal? nota = null;
            string feedback = "";
            DateTime? dataCorrecao = null;

            if (resp is not null && correcaoMap.TryGetValue(resp.Id, out var c))
            {
                corrigida = true;
                nota = c.Nota;
                feedback = c.Feedback ?? "";
                dataCorrecao = c.DataCorrecao;

                pesoCorrigido += t.Peso;
                soma += c.Nota * t.Peso;
            }

            itens.Add(new BoletimItemDto(
                t.Id,
                t.Titulo,
                t.DataEntrega,
                t.Peso,
                enviada,
                dataEnvio,
                corrigida,
                nota,
                feedback,
                dataCorrecao
            ));
        }

        decimal? media = null;
        if (pesoCorrigido > 0m)
            media = Round2(soma / pesoCorrigido);

        var totalTarefas = tarefas.Count;
        var tarefasEnviadas = respostas.Select(r => r.TarefaId).Distinct().Count();
        var tarefasCorrigidas = correcoes.Count;

        var (status, notaFinal) = CalcularStatus(media, freqPct, oa.NotaExame);

        return Ok(new BoletimOfertaDto(
            oferta.Id,
            oferta.DisciplinaCodigo,
            oferta.DisciplinaNome,
            oferta.CargaHoraria,
            oferta.Ano,
            oferta.Semestre,
            oferta.Periodo,

            aluno.Id,
            aluno.Matricula,
            aluno.Nome,

            media,
            totalTarefas,
            tarefasEnviadas,
            tarefasCorrigidas,
            Round2(pesoTotal),
            Round2(pesoCorrigido),

            totalAulas,
            faltas,
            presencas,
            freqPct,

            oa.NotaExame,
            status,
            notaFinal,

            itens
        ));
    }

    // ALUNO: detalhar minha oferta
    [HttpGet("ofertas/{ofertaId:int}/me")]
    [Authorize(Roles = "ALUNO")]
    public async Task<ActionResult<BoletimOfertaDto>> GetMinhaOferta(int ofertaId)
    {
        var userId = GetUserId();

        var aluno = await _db.Alunos.AsNoTracking()
            .FirstOrDefaultAsync(a => a.UsuarioId == userId);

        if (aluno is null)
            return NotFound("Aluno não encontrado.");

        // reaproveita o endpoint do professor (mesma lógica), mas sem precisar ser dono da oferta
        // aqui duplicamos a verificação de professorId: removida
        var oferta = await (
            from o in _db.OfertaDisciplinas.AsNoTracking()
            join d in _db.Disciplinas.AsNoTracking() on o.DisciplinaId equals d.Id
            where o.Id == ofertaId && o.Ativa
            select new
            {
                o.Id,
                DisciplinaCodigo = d.Codigo,
                DisciplinaNome = d.Nome,
                CargaHoraria = d.CargaHoraria,
                o.Ano,
                o.Semestre,
                o.Periodo,
                TotalAulas = o.TotalAulas
            }
        ).FirstOrDefaultAsync();

        if (oferta is null)
            return NotFound("Oferta não encontrada.");

        var oa = await _db.OfertaAlunos.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OfertaDisciplinaId == ofertaId && x.AlunoId == aluno.Id && x.Ativo);

        if (oa is null)
            return NotFound("Você não está vinculado a esta oferta.");

        var totalAulas = oferta.TotalAulas <= 0 ? 16 : oferta.TotalAulas;

        var faltas = await _db.FaltaOfertaAlunos.AsNoTracking()
            .CountAsync(f => f.Ativa && f.OfertaDisciplinaId == ofertaId && f.AlunoId == aluno.Id);

        if (faltas > totalAulas) faltas = totalAulas;

        var presencas = totalAulas - faltas;
        var freqPct = totalAulas == 0 ? 0m : Round2((decimal)presencas * 100m / totalAulas);

        var alunoInfo = await (
            from a in _db.Alunos.AsNoTracking()
            join u in _db.Usuarios.AsNoTracking() on a.UsuarioId equals u.Id
            where a.Id == aluno.Id
            select new { a.Id, a.Matricula, Nome = u.Nome }
        ).FirstAsync();

        var tarefas = await _db.Tarefas.AsNoTracking()
            .Where(t => t.OfertaDisciplinaId == ofertaId && t.Ativa)
            .Select(t => new { t.Id, t.Titulo, t.DataEntrega, t.Peso })
            .ToListAsync();

        var tarefaIds = tarefas.Select(t => t.Id).ToList();

        var respostas = await _db.TarefaRespostas.AsNoTracking()
            .Where(r => r.Ativa && r.AlunoId == aluno.Id && tarefaIds.Contains(r.TarefaId))
            .Select(r => new { r.Id, r.TarefaId, r.DataEnvio })
            .ToListAsync();

        var respostaIds = respostas.Select(r => r.Id).ToList();

        var correcoes = await _db.TarefaCorrecoes.AsNoTracking()
            .Where(c => c.Ativa && respostaIds.Contains(c.TarefaRespostaId))
            .Select(c => new { c.TarefaRespostaId, c.Nota, c.Feedback, c.DataCorrecao })
            .ToListAsync();

        var correcaoMap = correcoes
            .GroupBy(x => x.TarefaRespostaId)
            .ToDictionary(g => g.Key, g => g.First());

        var itens = new List<BoletimItemDto>();

        decimal pesoTotal = tarefas.Sum(t => t.Peso);
        decimal pesoCorrigido = 0m;
        decimal soma = 0m;

        foreach (var t in tarefas.OrderBy(x => x.DataEntrega).ThenBy(x => x.Titulo))
        {
            var resp = respostas.FirstOrDefault(r => r.TarefaId == t.Id);

            var enviada = resp is not null;
            var dataEnvio = resp?.DataEnvio;

            var corrigida = false;
            decimal? nota = null;
            string feedback = "";
            DateTime? dataCorrecao = null;

            if (resp is not null && correcaoMap.TryGetValue(resp.Id, out var c))
            {
                corrigida = true;
                nota = c.Nota;
                feedback = c.Feedback ?? "";
                dataCorrecao = c.DataCorrecao;

                pesoCorrigido += t.Peso;
                soma += c.Nota * t.Peso;
            }

            itens.Add(new BoletimItemDto(
                t.Id,
                t.Titulo,
                t.DataEntrega,
                t.Peso,
                enviada,
                dataEnvio,
                corrigida,
                nota,
                feedback,
                dataCorrecao
            ));
        }

        decimal? media = null;
        if (pesoCorrigido > 0m)
            media = Round2(soma / pesoCorrigido);

        var totalTarefas = tarefas.Count;
        var tarefasEnviadas = respostas.Select(r => r.TarefaId).Distinct().Count();
        var tarefasCorrigidas = correcoes.Count;

        var (status, notaFinal) = CalcularStatus(media, freqPct, oa.NotaExame);

        return Ok(new BoletimOfertaDto(
            oferta.Id,
            oferta.DisciplinaCodigo,
            oferta.DisciplinaNome,
            oferta.CargaHoraria,
            oferta.Ano,
            oferta.Semestre,
            oferta.Periodo,

            alunoInfo.Id,
            alunoInfo.Matricula,
            alunoInfo.Nome,

            media,
            totalTarefas,
            tarefasEnviadas,
            tarefasCorrigidas,
            Round2(pesoTotal),
            Round2(pesoCorrigido),

            totalAulas,
            faltas,
            presencas,
            freqPct,

            oa.NotaExame,
            status,
            notaFinal,

            itens
        ));
    }

    // PROFESSOR: setar total de aulas da oferta (padrão 16, professor pode alterar)
    [HttpPut("ofertas/{ofertaId:int}/total-aulas")]
    [Authorize(Roles = "PROFESSOR")]
    public async Task<ActionResult> SetTotalAulas(int ofertaId, [FromQuery] int totalAulas)
    {
        if (totalAulas <= 0 || totalAulas > 200)
            return BadRequest("Total de aulas inválido (1..200).");

        var userId = GetUserId();

        var prof = await _db.Professores
            .FirstOrDefaultAsync(p => p.UsuarioId == userId);

        if (prof is null)
            return NotFound("Professor não encontrado.");

        var oferta = await _db.OfertaDisciplinas
            .FirstOrDefaultAsync(o => o.Id == ofertaId && o.ProfessorId == prof.Id);

        if (oferta is null)
            return NotFound("Oferta não encontrada ou não pertence ao professor.");

        oferta.TotalAulas = totalAulas;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // PROFESSOR: marcar falta em uma aula (1..TotalAulas)
    [HttpPost("ofertas/{ofertaId:int}/alunos/{alunoId:int}/faltas/{numeroAula:int}")]
    [Authorize(Roles = "PROFESSOR")]
    public async Task<ActionResult> MarcarFalta(int ofertaId, int alunoId, int numeroAula)
    {
        var userId = GetUserId();

        var prof = await _db.Professores
            .FirstOrDefaultAsync(p => p.UsuarioId == userId);

        if (prof is null)
            return NotFound("Professor não encontrado.");

        var oferta = await _db.OfertaDisciplinas.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == ofertaId && o.ProfessorId == prof.Id);

        if (oferta is null)
            return NotFound("Oferta não encontrada ou não pertence ao professor.");

        var totalAulas = oferta.TotalAulas <= 0 ? 16 : oferta.TotalAulas;

        if (numeroAula < 1 || numeroAula > totalAulas)
            return BadRequest($"Número da aula inválido (1..{totalAulas}).");

        var oa = await _db.OfertaAlunos
            .FirstOrDefaultAsync(x => x.OfertaDisciplinaId == ofertaId && x.AlunoId == alunoId && x.Ativo);

        if (oa is null)
            return NotFound("Aluno não está vinculado a esta oferta.");

        var exists = await _db.OfertaAlunoFaltas
            .AnyAsync(f => f.OfertaAlunoId == oa.Id && f.NumeroAula == numeroAula);

        if (exists)
            return Conflict("Falta já marcada para esta aula.");

        _db.OfertaAlunoFaltas.Add(new OfertaAlunoFalta
        {
            OfertaAlunoId = oa.Id,
            NumeroAula = numeroAula
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // PROFESSOR: desmarcar falta
    [HttpDelete("ofertas/{ofertaId:int}/alunos/{alunoId:int}/faltas/{numeroAula:int}")]
    [Authorize(Roles = "PROFESSOR")]
    public async Task<ActionResult> DesmarcarFalta(int ofertaId, int alunoId, int numeroAula)
    {
        var userId = GetUserId();

        var prof = await _db.Professores
            .FirstOrDefaultAsync(p => p.UsuarioId == userId);

        if (prof is null)
            return NotFound("Professor não encontrado.");

        var oferta = await _db.OfertaDisciplinas.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == ofertaId && o.ProfessorId == prof.Id);

        if (oferta is null)
            return NotFound("Oferta não encontrada ou não pertence ao professor.");

        var oa = await _db.OfertaAlunos
            .FirstOrDefaultAsync(x => x.OfertaDisciplinaId == ofertaId && x.AlunoId == alunoId && x.Ativo);

        if (oa is null)
            return NotFound("Aluno não está vinculado a esta oferta.");

        var row = await _db.OfertaAlunoFaltas
            .FirstOrDefaultAsync(f => f.OfertaAlunoId == oa.Id && f.NumeroAula == numeroAula);

        if (row is null)
            return NotFound("Falta não encontrada.");

        _db.OfertaAlunoFaltas.Remove(row);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // PROFESSOR: setar nota do exame (para cálculo de nota final quando status = Exame)
    [HttpPut("ofertas/{ofertaId:int}/alunos/{alunoId:int}/nota-exame")]
    [Authorize(Roles = "PROFESSOR")]
    public async Task<ActionResult> SetNotaExame(int ofertaId, int alunoId, [FromQuery] decimal notaExame)
    {
        if (notaExame < 0m || notaExame > 10m)
            return BadRequest("Nota do exame inválida (0..10).");

        var userId = GetUserId();

        var prof = await _db.Professores
            .FirstOrDefaultAsync(p => p.UsuarioId == userId);

        if (prof is null)
            return NotFound("Professor não encontrado.");

        var ofertaOk = await _db.OfertaDisciplinas.AsNoTracking()
            .AnyAsync(o => o.Id == ofertaId && o.ProfessorId == prof.Id);

        if (!ofertaOk)
            return NotFound("Oferta não encontrada ou não pertence ao professor.");

        var oa = await _db.OfertaAlunos
            .FirstOrDefaultAsync(x => x.OfertaDisciplinaId == ofertaId && x.AlunoId == alunoId && x.Ativo);

        if (oa is null)
            return NotFound("Aluno não está vinculado a esta oferta.");

        oa.NotaExame = notaExame;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Roles = "ALUNO")]
    [HttpGet("me/pdf")]
    public async Task<IActionResult> GetPdfMe()
    {
        var userId = GetUserId();

        var alunoInfo = await _db.Alunos.AsNoTracking()
            .Include(a => a.Usuario)
            .Where(a => a.UsuarioId == userId)
            .Select(a => new { a.Id, a.Matricula, Nome = a.Usuario.Nome })
            .FirstOrDefaultAsync();

        if (alunoInfo is null) return Unauthorized();

        var turmaNome = await (
            from ta in _db.TurmaAlunos.AsNoTracking()
            join t in _db.Turmas.AsNoTracking() on ta.TurmaId equals t.Id
            where ta.AlunoId == alunoInfo.Id && ta.Ativo
            select t.Nome
        ).FirstOrDefaultAsync() ?? "-";

        var ofertaIds = await _db.OfertaAlunos.AsNoTracking()
            .Where(oa => oa.AlunoId == alunoInfo.Id && oa.Ativo)
            .Select(oa => oa.OfertaDisciplinaId)
            .Distinct()
            .ToListAsync();

        var ofertas = new List<BoletimOfertaDto>();
        foreach (var ofertaId in ofertaIds)
        {
            var dto = await MontarBoletimOfertaAlunoInternal(ofertaId, alunoInfo.Id);
            if (dto != null) ofertas.Add(dto);
        }

        var doc = new BoletimPdfDocument(alunoInfo.Nome, alunoInfo.Matricula, turmaNome, ofertas);
        var pdfBytes = doc.GeneratePdf();

        var fileName = $"boletim-{alunoInfo.Matricula}-{DateTime.Now:yyyyMMddHHmm}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }

    // ADMIN: PDF do boletim de um aluno específico
    [Authorize(Roles = "ADMIN")]
    [HttpGet("alunos/{alunoId:int}/pdf")]
    public async Task<IActionResult> GetPdfAlunoAdmin(int alunoId)
    {
        var alunoInfo = await _db.Alunos.AsNoTracking()
            .Include(a => a.Usuario)
            .Where(a => a.Id == alunoId)
            .Select(a => new { a.Id, a.Matricula, Nome = a.Usuario.Nome })
            .FirstOrDefaultAsync();

        if (alunoInfo is null) return NotFound("Aluno não encontrado.");

        var turmaNome = await (
            from ta in _db.TurmaAlunos.AsNoTracking()
            join t in _db.Turmas.AsNoTracking() on ta.TurmaId equals t.Id
            where ta.AlunoId == alunoInfo.Id && ta.Ativo
            select t.Nome
        ).FirstOrDefaultAsync() ?? "-";

        var ofertaIds = await _db.OfertaAlunos.AsNoTracking()
            .Where(oa => oa.AlunoId == alunoInfo.Id && oa.Ativo)
            .Select(oa => oa.OfertaDisciplinaId)
            .Distinct()
            .ToListAsync();

        var ofertas = new List<BoletimOfertaDto>();
        foreach (var ofertaId in ofertaIds)
        {
            var dto = await MontarBoletimOfertaAlunoInternal(ofertaId, alunoInfo.Id);
            if (dto != null) ofertas.Add(dto);
        }

        var doc = new BoletimPdfDocument(alunoInfo.Nome, alunoInfo.Matricula, turmaNome, ofertas);
        var pdfBytes = doc.GeneratePdf();

        var fileName = $"boletim-{alunoInfo.Matricula}-{DateTime.Now:yyyyMMddHHmm}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }

    // Reaproveita a mesma estrutura do BoletimOfertaDto completo (com frequência + status)
    // Retorna null se a oferta/aluno não existirem ou se o aluno não estiver vinculado na oferta.
    private async Task<BoletimOfertaDto?> MontarBoletimOfertaAlunoInternal(int ofertaId, int alunoId)
    {
        // Oferta + disciplina
        var oferta = await (
            from o in _db.OfertaDisciplinas.AsNoTracking()
            join d in _db.Disciplinas.AsNoTracking() on o.DisciplinaId equals d.Id
            where o.Id == ofertaId && o.Ativa
            select new
            {
                o.Id,
                DisciplinaCodigo = d.Codigo,
                DisciplinaNome = d.Nome,
                CargaHoraria = d.CargaHoraria,
                o.Ano,
                o.Semestre,
                o.Periodo,
                TotalAulas = o.TotalAulas
            }
        ).FirstOrDefaultAsync();

        if (oferta is null) return null;

        // Aluno + usuário
        var aluno = await (
            from a in _db.Alunos.AsNoTracking()
            join u in _db.Usuarios.AsNoTracking() on a.UsuarioId equals u.Id
            where a.Id == alunoId
            select new { a.Id, a.Matricula, Nome = u.Nome }
        ).FirstOrDefaultAsync();

        if (aluno is null) return null;

        // Vinculo do aluno na oferta (para NotaExame e para contar faltas)
        var oa = await _db.OfertaAlunos.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OfertaDisciplinaId == ofertaId && x.AlunoId == alunoId && x.Ativo);

        if (oa is null) return null;

        // Frequência (faltas por aula marcada)
        var totalAulas = oferta.TotalAulas <= 0 ? 16 : oferta.TotalAulas;

        var faltas = await _db.FaltaOfertaAlunos.AsNoTracking()
            .CountAsync(f => f.Ativa && f.OfertaDisciplinaId == ofertaId && f.AlunoId == alunoId);

        if (faltas > totalAulas) faltas = totalAulas;

        var presencas = totalAulas - faltas;
        var freqPct = totalAulas == 0 ? 0m : Round2((decimal)presencas * 100m / totalAulas);

        // Tarefas da oferta
        var tarefas = await _db.Tarefas.AsNoTracking()
            .Where(t => t.OfertaDisciplinaId == ofertaId && t.Ativa)
            .Select(t => new { t.Id, t.Titulo, t.DataEntrega, t.Peso })
            .ToListAsync();

        var tarefaIds = tarefas.Select(t => t.Id).ToList();

        // Respostas do aluno (1 por tarefa, já que você bloqueou reentrega)
        var respostas = await _db.TarefaRespostas.AsNoTracking()
            .Where(r => r.Ativa && r.AlunoId == alunoId && tarefaIds.Contains(r.TarefaId))
            .Select(r => new { r.Id, r.TarefaId, r.DataEnvio })
            .ToListAsync();

        var respostaIds = respostas.Select(r => r.Id).ToList();

        // Correções das respostas
        var correcoes = await _db.TarefaCorrecoes.AsNoTracking()
            .Where(c => c.Ativa && respostaIds.Contains(c.TarefaRespostaId))
            .Select(c => new { c.TarefaRespostaId, c.Nota, c.Feedback, c.DataCorrecao })
            .ToListAsync();

        var correcaoMap = correcoes
            .GroupBy(x => x.TarefaRespostaId)
            .ToDictionary(g => g.Key, g => g.First());

        // Itens do boletim
        var itens = new List<BoletimItemDto>();

        decimal pesoTotal = tarefas.Sum(t => t.Peso);
        decimal pesoCorrigido = 0m;
        decimal soma = 0m;

        foreach (var t in tarefas.OrderBy(x => x.DataEntrega).ThenBy(x => x.Titulo))
        {
            var resp = respostas.FirstOrDefault(r => r.TarefaId == t.Id);

            var enviada = resp is not null;
            var dataEnvio = resp?.DataEnvio;

            var corrigida = false;
            decimal? nota = null;
            string feedback = "";
            DateTime? dataCorrecao = null;

            if (resp is not null && correcaoMap.TryGetValue(resp.Id, out var c))
            {
                corrigida = true;
                nota = c.Nota;
                feedback = c.Feedback ?? "";
                dataCorrecao = c.DataCorrecao;

                pesoCorrigido += t.Peso;
                soma += c.Nota * t.Peso;
            }

            itens.Add(new BoletimItemDto(
                t.Id,
                t.Titulo,
                t.DataEntrega,
                t.Peso,
                enviada,
                dataEnvio,
                corrigida,
                nota,
                feedback,
                dataCorrecao
            ));
        }

        // Médias/contagens
        decimal? media = null;
        if (pesoCorrigido > 0m)
            media = Round2(soma / pesoCorrigido);

        var totalTarefas = tarefas.Count;
        var tarefasEnviadas = respostas.Select(r => r.TarefaId).Distinct().Count();
        var tarefasCorrigidas = correcoes.Count;

        // Status/nota final (considera freq + exame)
        var (status, notaFinal) = CalcularStatus(media, freqPct, oa.NotaExame);

        return new BoletimOfertaDto(
            oferta.Id,
            oferta.DisciplinaCodigo,
            oferta.DisciplinaNome,
            oferta.CargaHoraria,
            oferta.Ano,
            oferta.Semestre,
            oferta.Periodo,

            aluno.Id,
            aluno.Matricula,
            aluno.Nome,

            media,
            totalTarefas,
            tarefasEnviadas,
            tarefasCorrigidas,
            Round2(pesoTotal),
            Round2(pesoCorrigido),

            totalAulas,
            faltas,
            presencas,
            freqPct,

            oa.NotaExame,
            status,
            notaFinal,

            itens
        );
    }

}

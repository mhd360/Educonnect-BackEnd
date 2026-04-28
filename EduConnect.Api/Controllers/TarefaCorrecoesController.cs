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
[Route("api/ofertas/{ofertaId:int}/tarefas/{tarefaId:int}/correcoes")]
public class TarefaCorrecoesController : ControllerBase
{
    private readonly AppDbContext _db;
    public TarefaCorrecoesController(AppDbContext db) => _db = db;

    // PROFESSOR: cria/atualiza correção para a resposta ativa do aluno
    // rota: POST /api/ofertas/{ofertaId}/tarefas/{tarefaId}/correcoes/{alunoId}
    [Authorize(Roles = "PROFESSOR")]
    [HttpPost("{alunoId:int}")]
    public async Task<ActionResult<TarefaCorrecaoReadDto>> Upsert(int ofertaId, int tarefaId, int alunoId, TarefaCorrecaoUpsertDto dto)
    {
        if (dto.Nota < 0 || dto.Nota > 10) return BadRequest("Nota deve estar entre 0 e 10.");

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        // pega professorId
        var professorId = await _db.Professores.AsNoTracking()
            .Where(p => p.UsuarioId == userId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        if (professorId == 0) return Unauthorized();

        // oferta pertence ao professor
        var ofertaOk = await _db.OfertaDisciplinas.AsNoTracking()
            .AnyAsync(o => o.Id == ofertaId && o.ProfessorId == professorId);

        if (!ofertaOk) return Forbid();

        // tarefa pertence à oferta
        var tarefaOk = await _db.Tarefas.AsNoTracking()
            .AnyAsync(t => t.Id == tarefaId && t.OfertaDisciplinaId == ofertaId);

        if (!tarefaOk) return NotFound("Tarefa não encontrada.");

        // acha resposta ativa do aluno para a tarefa
        var respostaId = await _db.TarefaRespostas.AsNoTracking()
            .Where(r => r.TarefaId == tarefaId && r.AlunoId == alunoId && r.Ativa)
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        if (respostaId == 0) return NotFound("Resposta ativa do aluno não encontrada.");

        using var tx = await _db.Database.BeginTransactionAsync();

        // desativa correção anterior (se existir)
        var antiga = await _db.TarefaCorrecoes
            .FirstOrDefaultAsync(c => c.TarefaRespostaId == respostaId && c.Ativa);

        if (antiga is not null)
            antiga.Ativa = false;

        var correcao = new TarefaCorrecao
        {
            TarefaRespostaId = respostaId,
            Nota = dto.Nota,
            Feedback = string.IsNullOrWhiteSpace(dto.Feedback) ? null : dto.Feedback.Trim(),
            Ativa = true
        };

        _db.TarefaCorrecoes.Add(correcao);
        await _db.SaveChangesAsync();

        await tx.CommitAsync();

        return Ok(new TarefaCorrecaoReadDto(
            correcao.Id,
            correcao.TarefaRespostaId,
            correcao.Nota,
            correcao.Feedback,
            correcao.DataCorrecao
        ));
    }

    // ALUNO: ver correção da própria resposta ativa
    // GET /api/ofertas/{ofertaId}/tarefas/{tarefaId}/correcoes/me
    [Authorize(Roles = "ALUNO")]
    [HttpGet("me")]
    public async Task<ActionResult<TarefaCorrecaoReadDto>> GetMinhaCorrecao(int ofertaId, int tarefaId)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        var alunoId = await _db.Alunos.AsNoTracking()
            .Where(a => a.UsuarioId == userId)
            .Select(a => a.Id)
            .FirstOrDefaultAsync();

        if (alunoId == 0) return Unauthorized();

        // valida matrícula na oferta
        var matriculado = await _db.OfertaAlunos.AsNoTracking()
            .AnyAsync(oa => oa.OfertaDisciplinaId == ofertaId && oa.AlunoId == alunoId && oa.Ativo);

        if (!matriculado) return Forbid();

        // acha resposta ativa
        var respostaId = await _db.TarefaRespostas.AsNoTracking()
            .Where(r => r.TarefaId == tarefaId && r.AlunoId == alunoId && r.Ativa)
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        if (respostaId == 0) return NotFound("Resposta não encontrada.");

        // busca correção ativa
        var correcao = await _db.TarefaCorrecoes.AsNoTracking()
            .Where(c => c.TarefaRespostaId == respostaId && c.Ativa)
            .Select(c => new TarefaCorrecaoReadDto(c.Id, c.TarefaRespostaId, c.Nota, c.Feedback, c.DataCorrecao))
            .FirstOrDefaultAsync();

        if (correcao is null) return NotFound();
        return Ok(correcao);
    }

    [Authorize(Roles = "PROFESSOR")]
    [HttpGet("/api/ofertas/{ofertaId:int}/tarefas/{tarefaId:int}/alunos/{alunoId:int}/correcao")]
    public async Task<ActionResult<TarefaCorrecaoProfessorAlunoDto>> GetCorrecaoDoAluno(
    int ofertaId, int tarefaId, int alunoId)
    {
        var userIdStr = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        var profId = await _db.Professores.AsNoTracking()
            .Where(p => p.UsuarioId == userId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        if (profId == 0) return Forbid();

        // Oferta deve ser do professor
        var ofertaOk = await _db.OfertaDisciplinas.AsNoTracking()
            .AnyAsync(o => o.Id == ofertaId && o.ProfessorId == profId && o.Ativa);

        if (!ofertaOk) return Forbid();

        // Tarefa deve pertencer à oferta
        var tarefaInfo = await _db.Tarefas.AsNoTracking()
            .Where(t => t.Id == tarefaId && t.OfertaDisciplinaId == ofertaId && t.Ativa)
            .Select(t => new { t.Id, t.Titulo })
            .FirstOrDefaultAsync();

        if (tarefaInfo is null) return NotFound("Tarefa não encontrada nesta oferta.");

        // Aluno deve estar matriculado na oferta
        var alunoInfo = await (
            from oa in _db.OfertaAlunos.AsNoTracking()
            join a in _db.Alunos.AsNoTracking() on oa.AlunoId equals a.Id
            join u in _db.Usuarios.AsNoTracking() on a.UsuarioId equals u.Id
            where oa.OfertaDisciplinaId == ofertaId && oa.AlunoId == alunoId && oa.Ativo
            select new { a.Id, a.Matricula, Nome = u.Nome }
        ).FirstOrDefaultAsync();

        if (alunoInfo is null) return NotFound("Aluno não está matriculado nesta oferta.");

        // Resposta do aluno para a tarefa (você bloqueou reentrega: 1 resposta ativa)
        var resposta = await _db.TarefaRespostas.AsNoTracking()
            .Where(r => r.TarefaId == tarefaId && r.AlunoId == alunoId && r.Ativa)
            .Select(r => new { r.Id, r.DataEnvio })
            .FirstOrDefaultAsync();

        if (resposta is null) return NotFound("Aluno ainda não enviou resposta.");

        // Correção da resposta
        var correcao = await _db.TarefaCorrecoes.AsNoTracking()
            .Where(c => c.TarefaRespostaId == resposta.Id && c.Ativa)
            .Select(c => new { c.Id, c.Nota, c.Feedback, c.DataCorrecao })
            .FirstOrDefaultAsync();

        if (correcao is null) return NotFound("Resposta ainda não foi corrigida.");

        return Ok(new TarefaCorrecaoProfessorAlunoDto(
            OfertaId: ofertaId,
            TarefaId: tarefaInfo.Id,
            TarefaTitulo: tarefaInfo.Titulo,
            AlunoId: alunoInfo.Id,
            AlunoNome: alunoInfo.Nome,
            AlunoMatricula: alunoInfo.Matricula,
            RespostaId: resposta.Id,
            DataEnvio: resposta.DataEnvio,
            CorrecaoId: correcao.Id,
            Nota: correcao.Nota,
            Feedback: correcao.Feedback,
            DataCorrecao: correcao.DataCorrecao
        ));
    }
}

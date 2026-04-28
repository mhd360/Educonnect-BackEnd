using EduConnect.Api.Data;
using EduConnect.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EduConnect.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/ofertas/{ofertaId:int}/tarefas/{tarefaId:int}/respostas")]
public class TarefaRespostasController : ControllerBase
{
    private readonly AppDbContext _db;
    public TarefaRespostasController(AppDbContext db) => _db = db;

    // ALUNO: enviar / reenviar resposta (mantém só 1 ativa)
    [Authorize(Roles = "ALUNO")]
    [HttpPost]
    public async Task<IActionResult> Enviar(int ofertaId, int tarefaId, TarefaRespostaCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Conteudo))
            return BadRequest("Conteúdo é obrigatório.");

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        // valida: aluno + matrícula na oferta + tarefa pertence à oferta
        var aluno = await _db.Alunos.AsNoTracking().FirstOrDefaultAsync(a => a.UsuarioId == userId);
        if (aluno is null) return Unauthorized();

        var matriculado = await _db.OfertaAlunos.AsNoTracking()
            .AnyAsync(oa => oa.OfertaDisciplinaId == ofertaId && oa.AlunoId == aluno.Id && oa.Ativo);

        if (!matriculado) return Forbid();

        var tarefaOk = await _db.Tarefas.AsNoTracking()
            .AnyAsync(t => t.Id == tarefaId && t.OfertaDisciplinaId == ofertaId && t.Ativa);

        if (!tarefaOk) return NotFound("Tarefa não encontrada.");

        var jaEnviou = await _db.TarefaRespostas.AsNoTracking()
        .AnyAsync(r => r.TarefaId == tarefaId && r.AlunoId == aluno.Id);

        if (jaEnviou)
            return Conflict("Resposta já enviada. Reentrega não é permitida.");

        _db.TarefaRespostas.Add(new EduConnect.Api.Domain.TarefaResposta
        {
            TarefaId = tarefaId,
            AlunoId = aluno.Id,
            Conteudo = dto.Conteudo.Trim(),
            Ativa = true
        });

        await _db.SaveChangesAsync();
        return NoContent();

    }

    // ALUNO: ver a própria resposta ativa
    [Authorize(Roles = "ALUNO")]
    [HttpGet("me")]
    public async Task<ActionResult<TarefaRespostaReadDto>> GetMinhaResposta(int ofertaId, int tarefaId)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        var aluno = await _db.Alunos.AsNoTracking().FirstOrDefaultAsync(a => a.UsuarioId == userId);
        if (aluno is null) return Unauthorized();

        // valida matrícula na oferta
        var matriculado = await _db.OfertaAlunos.AsNoTracking()
            .AnyAsync(oa => oa.OfertaDisciplinaId == ofertaId && oa.AlunoId == aluno.Id && oa.Ativo);

        if (!matriculado) return Forbid();

        var resp = await _db.TarefaRespostas.AsNoTracking()
            .Where(r => r.TarefaId == tarefaId && r.AlunoId == aluno.Id && r.Ativa)
            .Join(_db.Alunos.AsNoTracking(), r => r.AlunoId, a => a.Id, (r, a) => new { r, a })
            .Join(_db.Usuarios.AsNoTracking(), x => x.a.UsuarioId, u => u.Id, (x, u) => new TarefaRespostaReadDto(
                x.r.Id,
                x.r.TarefaId,
                x.r.AlunoId,
                x.a.Matricula,
                u.Nome,
                x.r.Conteudo,
                x.r.DataEnvio
            ))
            .FirstOrDefaultAsync();

        if (resp is null) return NotFound();
        return Ok(resp);
    }

    // PROFESSOR: listar respostas da tarefa (somente da própria oferta)
    [Authorize(Roles = "PROFESSOR")]
    [HttpGet]
    public async Task<IActionResult> Listar(int ofertaId, int tarefaId, [FromQuery] bool includeInativas = false)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        // 1) Descobre o ProfessorId do usuário logado
        var professorId = await _db.Professores
            .AsNoTracking()
            .Where(p => p.UsuarioId == userId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        if (professorId == 0) return Unauthorized();

        // 2) Valida que a oferta pertence a esse professor
        var ofertaOk = await _db.OfertaDisciplinas
            .AsNoTracking()
            .AnyAsync(o => o.Id == ofertaId && o.ProfessorId == professorId);

        if (!ofertaOk) return Forbid();

        // 3) Valida que a tarefa pertence à oferta
        var tarefaOk = await _db.Tarefas
            .AsNoTracking()
            .AnyAsync(t => t.Id == tarefaId && t.OfertaDisciplinaId == ofertaId);

        if (!tarefaOk) return NotFound("Tarefa não encontrada.");

        // 4) Lista respostas
        var query = _db.TarefaRespostas
    .AsNoTracking()
    .Where(r => r.TarefaId == tarefaId);

        if (!includeInativas)
            query = query.Where(r => r.Ativa);

        var list = await query
            .Join(_db.Alunos.AsNoTracking(),
                r => r.AlunoId,
                a => a.Id,
                (r, a) => new { r, a })
            .Join(_db.Usuarios.AsNoTracking(),
                x => x.a.UsuarioId,
                u => u.Id,
                (x, u) => new { x.r, x.a, u })
            // ORDER BY ANTES do Select para DTO
            .OrderBy(x => x.a.Matricula)
            .ThenByDescending(x => x.r.DataEnvio)
            .Select(x => new TarefaRespostaReadDto(
                x.r.Id,
                x.r.TarefaId,
                x.r.AlunoId,
                x.a.Matricula,
                x.u.Nome,
                x.r.Conteudo,
                x.r.DataEnvio
            ))
            .ToListAsync();

        return Ok(list);
    }

}

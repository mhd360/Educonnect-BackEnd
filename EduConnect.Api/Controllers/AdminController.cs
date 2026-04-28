using EduConnect.Api.Data;
using EduConnect.Api.Domain;
using EduConnect.Api.DTOs;
using MailKit.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MailKit.Net.Smtp;
using MimeKit;
using System.Security.Cryptography;
using System.Text;

namespace EduConnect.Api.Controllers;

[Authorize(Roles = "ADMIN")]
[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;

    private readonly PasswordHasher<Usuario> _hasher = new();
    public AdminController(AppDbContext db) => _db = db;

    // auxiliar tipado (EF consegue projetar)
    private sealed record PessoaBase(int Id, string Matricula, int UsuarioId, string UsuarioCpf);

    // senha provisória
    private static string GerarSenhaProvisoria(int length = 10)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789@#";
        var bytes = RandomNumberGenerator.GetBytes(length);
        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
            sb.Append(chars[bytes[i] % chars.Length]);
        return sb.ToString();
    }

    [HttpGet("resumo")]
    public async Task<ActionResult<AdminResumoDto>> Resumo()
    {
        var totalUsuarios = await _db.Usuarios.AsNoTracking().CountAsync();
        var totalAlunos = await _db.Alunos.AsNoTracking().CountAsync();
        var totalProfessores = await _db.Professores.AsNoTracking().CountAsync();
        var totalTurmas = await _db.Turmas.AsNoTracking().CountAsync();
        var totalDisciplinas = await _db.Disciplinas.AsNoTracking().CountAsync();
        var totalOfertas = await _db.OfertaDisciplinas.AsNoTracking().CountAsync();

        var totalTarefasAtivas = await _db.Tarefas.AsNoTracking().CountAsync(t => t.Ativa);
        var totalRespostasAtivas = await _db.TarefaRespostas.AsNoTracking().CountAsync(r => r.Ativa);

        var totalPendentesCorrecao = await _db.TarefaRespostas.AsNoTracking()
            .Where(r => r.Ativa)
            .CountAsync(r => !_db.TarefaCorrecoes.Any(c => c.Ativa && c.TarefaRespostaId == r.Id));

        return Ok(new AdminResumoDto(
            totalUsuarios,
            totalAlunos,
            totalProfessores,
            totalTurmas,
            totalDisciplinas,
            totalOfertas,
            totalTarefasAtivas,
            totalRespostasAtivas,
            totalPendentesCorrecao
        ));
    }

    [HttpGet("alunos")]
    public async Task<ActionResult<PagedResultDto<AdminPessoaListItemDto>>> ListarAlunos(
    [FromQuery] string? search = null,
    [FromQuery] int skip = 0,
    [FromQuery] int limit = 50)
    {
        if (skip < 0) skip = 0;
        if (limit <= 0) limit = 50;
        if (limit > 200) limit = 200;

        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        int total;
        List<PessoaBase> alunosBase;

        if (search is null)
        {
            total = await _db.Alunos.AsNoTracking().CountAsync();

            alunosBase = await _db.Alunos.AsNoTracking()
                .OrderBy(a => a.Matricula)
                .Skip(skip)
                .Take(limit)
                .Select(a => new PessoaBase(a.Id, a.Matricula, a.UsuarioId, a.Usuario.Cpf ))
                .ToListAsync();
        }
        else
        {
            var byMat = await _db.Alunos.AsNoTracking()
                .Where(a => a.Matricula.Contains(search))
                .Select(a => new PessoaBase(a.Id, a.Matricula, a.UsuarioId, a.Usuario.Cpf))
                .ToListAsync();

            var userIdsByNome = await _db.Usuarios.AsNoTracking()
                .Where(u => u.Nome.Contains(search))
                .Select(u => u.Id)
                .Take(2000)
                .ToListAsync();

            var byNome = userIdsByNome.Count == 0
                ? new List<PessoaBase>()
                : await _db.Alunos.AsNoTracking()
                    .Where(a => userIdsByNome.Contains(a.UsuarioId))
                    .Select(a => new PessoaBase(a.Id, a.Matricula, a.UsuarioId, a.Usuario.Cpf))
                    .ToListAsync();

            var merged = byMat
                .Concat(byNome)
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .OrderBy(x => x.Matricula)
                .ToList();

            total = merged.Count;

            alunosBase = merged
                .Skip(skip)
                .Take(limit)
                .ToList();
        }

        if (alunosBase.Count == 0)
            return Ok(new PagedResultDto<AdminPessoaListItemDto>(total, new List<AdminPessoaListItemDto>()));

        var userIds = alunosBase.Select(x => x.UsuarioId).Distinct().ToList();

        var users = await _db.Usuarios.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Nome, u.Ativo })
            .ToListAsync();

        var userMap = users.ToDictionary(x => x.Id, x => (Nome: x.Nome, Ativo: x.Ativo));

        var items = alunosBase
            .Select(a =>
            {
                var info = userMap.TryGetValue(a.UsuarioId, out var u) ? u : (Nome: "", Ativo: false);
                return new AdminPessoaListItemDto(a.Id, a.Matricula, info.Nome, a.UsuarioCpf, info.Ativo);
            })
            .ToList();

        return Ok(new PagedResultDto<AdminPessoaListItemDto>(total, items));
    }



    [HttpGet("professores")]
    public async Task<ActionResult<PagedResultDto<AdminPessoaListItemDto>>> ListarProfessores(
    [FromQuery] string? search = null,
    [FromQuery] int skip = 0,
    [FromQuery] int limit = 50)
    {
        if (skip < 0) skip = 0;
        if (limit <= 0) limit = 50;
        if (limit > 200) limit = 200;

        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        int total;
        List<PessoaBase> profsBase;

        if (search is null)
        {
            total = await _db.Professores.AsNoTracking().CountAsync();

            profsBase = await _db.Professores.AsNoTracking()
                .OrderBy(p => p.Matricula)
                .Skip(skip)
                .Take(limit)
                .Select(p => new PessoaBase(p.Id, p.Matricula, p.UsuarioId, p.Usuario.Cpf))
                .ToListAsync();
        }
        else
        {
            var byMat = await _db.Professores.AsNoTracking()
                .Where(p => p.Matricula.Contains(search))
                .Select(p => new PessoaBase(p.Id, p.Matricula, p.UsuarioId, p.Usuario.Cpf))
                .ToListAsync();

            var userIdsByNome = await _db.Usuarios.AsNoTracking()
                .Where(u => u.Nome.Contains(search))
                .Select(u => u.Id)
                .Take(2000)
                .ToListAsync();

            var byNome = userIdsByNome.Count == 0
                ? new List<PessoaBase>()
                : await _db.Professores.AsNoTracking()
                    .Where(p => userIdsByNome.Contains(p.UsuarioId))
                    .Select(p => new PessoaBase(p.Id, p.Matricula, p.UsuarioId, p.Usuario.Cpf))
                    .ToListAsync();

            var merged = byMat
                .Concat(byNome)
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .OrderBy(x => x.Matricula)
                .ToList();

            total = merged.Count;

            profsBase = merged
                .Skip(skip)
                .Take(limit)
                .ToList();
        }

        if (profsBase.Count == 0)
            return Ok(new PagedResultDto<AdminPessoaListItemDto>(total, new List<AdminPessoaListItemDto>()));

        var userIds = profsBase.Select(x => x.UsuarioId).Distinct().ToList();

        var users = await _db.Usuarios.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Nome, u.Cpf, u.Ativo })
            .ToListAsync();

        var userMap = users.ToDictionary(x => x.Id, x => (Nome: x.Nome, Cpf: x.Cpf, Ativo: x.Ativo));

        var items = profsBase
            .Select(p =>
            {
                var info = userMap.TryGetValue(p.UsuarioId, out var u) ? u : (Nome: "", Cpf: "", Ativo: false);
                return new AdminPessoaListItemDto(p.Id, p.Matricula, info.Nome, info.Cpf, info.Ativo);
            })
            .ToList();

        return Ok(new PagedResultDto<AdminPessoaListItemDto>(total, items));
    }



    [HttpGet("alunos/{id:int}")]
    public async Task<ActionResult<AdminAlunoDetalheDto>> AlunoDetalhe(int id)
    {
        var aluno = await _db.Alunos.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new { a.Id, a.Matricula, a.UsuarioId })
            .FirstOrDefaultAsync();

        if (aluno is null) return NotFound();

        var user = await _db.Usuarios.AsNoTracking()
            .Where(u => u.Id == aluno.UsuarioId)
            .Select(u => new { u.Nome, u.Ativo })
            .FirstOrDefaultAsync();

        if (user is null) return NotFound();

        // ofertas do aluno -> ids
        var ofertaIds = await _db.OfertaAlunos.AsNoTracking()
            .Where(oa => oa.Ativo && oa.AlunoId == id)
            .Select(oa => oa.OfertaDisciplinaId)
            .Distinct()
            .ToListAsync();

        var ofertas = ofertaIds.Count == 0
            ? new List<AdminOfertaResumoDto>()
            : await MontarResumoOfertas(ofertaIds);

        return Ok(new AdminAlunoDetalheDto(
            aluno.Id,
            aluno.Matricula,
            user.Nome,
            user.Ativo,
            ofertas
        ));
    }

    [HttpGet("professores/{id:int}")]
    public async Task<ActionResult<AdminProfessorDetalheDto>> ProfessorDetalhe(int id)
    {
        var prof = await _db.Professores.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new { p.Id, p.Matricula, p.UsuarioId })
            .FirstOrDefaultAsync();

        if (prof is null) return NotFound();

        var user = await _db.Usuarios.AsNoTracking()
            .Where(u => u.Id == prof.UsuarioId)
            .Select(u => new { u.Nome, u.Ativo })
            .FirstOrDefaultAsync();

        if (user is null) return NotFound();

        var ofertaIds = await _db.OfertaDisciplinas.AsNoTracking()
            .Where(o => o.Ativa && o.ProfessorId == id)
            .Select(o => o.Id)
            .Distinct()
            .ToListAsync();

        var ofertas = ofertaIds.Count == 0
            ? new List<AdminOfertaResumoDto>()
            : await MontarResumoOfertas(ofertaIds);

        return Ok(new AdminProfessorDetalheDto(
            prof.Id,
            prof.Matricula,
            user.Nome,
            user.Ativo,
            ofertas
        ));
    }

    private async Task<List<AdminOfertaResumoDto>> MontarResumoOfertas(List<int> ofertaIds)
    {
        // 1) Ofertas (já “normaliza” possíveis int? para int)
        var ofertas = await _db.OfertaDisciplinas.AsNoTracking()
            .Where(o => ofertaIds.Contains(o.Id))
            .Select(o => new
            {
                o.Id,
                TurmaId = (int?)o.TurmaId ?? 0,
                DisciplinaId = (int?)o.DisciplinaId ?? 0,
                ProfessorId = (int?)o.ProfessorId ?? 0
            })
            .ToListAsync();

        if (ofertas.Count == 0) return new List<AdminOfertaResumoDto>();

        var turmaIds = ofertas.Select(x => x.TurmaId).Where(x => x != 0).Distinct().ToList();
        var discIds = ofertas.Select(x => x.DisciplinaId).Where(x => x != 0).Distinct().ToList();
        var profIds = ofertas.Select(x => x.ProfessorId).Where(x => x != 0).Distinct().ToList();

        // 2) Turmas
        var turmas = await _db.Turmas.AsNoTracking()
            .Where(t => turmaIds.Contains(t.Id))
            .Select(t => new
            {
                t.Id,
                t.Nome,
                t.Ano,
                t.Semestre,
                Periodo = t.Periodo.ToString()
            })
            .ToListAsync();
        var turmaMap = turmas.ToDictionary(x => x.Id, x => x);

        // 3) Disciplinas
        var discs = await _db.Disciplinas.AsNoTracking()
            .Where(d => discIds.Contains(d.Id))
            .Select(d => new { d.Id, d.Codigo, d.Nome })
            .ToListAsync();
        var discMap = discs.ToDictionary(x => x.Id, x => x);

        // 4) Professores + nomes (Usuários)
        var profs = await _db.Professores.AsNoTracking()
            .Where(p => profIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Matricula, p.UsuarioId })
            .ToListAsync();

        var profUserIds = profs.Select(x => x.UsuarioId).Distinct().ToList();

        var users = await _db.Usuarios.AsNoTracking()
            .Where(u => profUserIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Nome })
            .ToListAsync();
        var userMap = users.ToDictionary(x => x.Id, x => x.Nome);

        var profMap = profs.ToDictionary(
            x => x.Id,
            x => (Matricula: x.Matricula, Nome: userMap.TryGetValue(x.UsuarioId, out var n) ? n : "")
        );

        // 5) Monta DTO final (sem quebrar se faltar algo)
        var result = ofertas
            .Select(o =>
            {
                turmaMap.TryGetValue(o.TurmaId, out var t);
                discMap.TryGetValue(o.DisciplinaId, out var d);
                var p = profMap.TryGetValue(o.ProfessorId, out var pp) ? pp : (Matricula: "", Nome: "");

                return new AdminOfertaResumoDto(
                    o.Id,
                    d?.Codigo ?? "",
                    d?.Nome ?? "",
                    t?.Nome ?? "",
                    t?.Ano ?? 0,
                    t?.Semestre ?? 0,
                    t?.Periodo ?? "",
                    p.Matricula,
                    p.Nome
                );
            })
            .OrderBy(x => x.Ano)
            .ThenBy(x => x.Semestre)
            .ThenBy(x => x.TurmaNome)
            .ThenBy(x => x.DisciplinaCodigo)
            .ToList();

        return result;
    }

    [HttpPost("usuarios")]
    public async Task<ActionResult<AdminCriarUsuarioResultDto>> CriarUsuario([FromBody] AdminCriarUsuarioDto dto)
    {
        // Normalização
        var nome = (dto.Nome ?? "").Trim();
        var email = (dto.Email ?? "").Trim().ToLowerInvariant();
        var cpf = new string((dto.Cpf ?? "").Where(char.IsDigit).ToArray());

        // Validações
        if (string.IsNullOrWhiteSpace(nome))
            return BadRequest("Nome é obrigatório.");

        if (string.IsNullOrWhiteSpace(email))
            return BadRequest("Email é obrigatório.");

        if (cpf.Length != 11 || cpf.All(ch => ch == cpf[0]))
            return BadRequest("CPF inválido.");

        // Unicidade
        var emailEmUso = await _db.Usuarios.AsNoTracking()
            .AnyAsync(u => u.Email.ToLower() == email);

        if (emailEmUso)
            return BadRequest("Email já cadastrado.");

        var cpfEmUso = await _db.Usuarios.AsNoTracking()
            .AnyAsync(u => u.Cpf == cpf);

        if (cpfEmUso)
            return BadRequest("CPF já cadastrado.");

        // ADMIN é único
        if (dto.Perfil == PerfilUsuario.ADMIN)
        {
            var jaExisteAdmin = await _db.Usuarios.AsNoTracking()
                .AnyAsync(u => u.Perfil == PerfilUsuario.ADMIN);

            if (jaExisteAdmin)
                return BadRequest("Já existe um administrador cadastrado.");
        }

        // Senha provisória (gerada, mas não exibida e não enviada)
        var senhaProvisoria = GerarSenhaProvisoria(10);

        // Cria usuário
        var user = new Usuario
        {
            Nome = nome,
            Email = email,
            Cpf = cpf,
            Perfil = dto.Perfil,
            Ativo = true,
            SenhaHash = ""
        };

        user.SenhaHash = _hasher.HashPassword(user, senhaProvisoria);

        _db.Usuarios.Add(user);
        await _db.SaveChangesAsync();

        // Cria aluno/professor conforme perfil (gera matrícula)
        string matricula;

        if (dto.Perfil == PerfilUsuario.ALUNO)
        {
            var last = await _db.Alunos.AsNoTracking()
                .OrderByDescending(a => a.Id)
                .Select(a => a.Matricula)
                .FirstOrDefaultAsync();

            var nextNum = 1;
            if (!string.IsNullOrWhiteSpace(last) && last.Length >= 7 && int.TryParse(last.Substring(1), out var n))
                nextNum = n + 1;

            matricula = "A" + nextNum.ToString("D6");

            _db.Alunos.Add(new Aluno { Matricula = matricula, UsuarioId = user.Id });
            await _db.SaveChangesAsync();
        }
        else if (dto.Perfil == PerfilUsuario.PROFESSOR)
        {
            var last = await _db.Professores.AsNoTracking()
                .OrderByDescending(p => p.Id)
                .Select(p => p.Matricula)
                .FirstOrDefaultAsync();

            var nextNum = 1;
            if (!string.IsNullOrWhiteSpace(last) && last.Length >= 7 && int.TryParse(last.Substring(1), out var n))
                nextNum = n + 1;

            matricula = "P" + nextNum.ToString("D6");

            _db.Professores.Add(new Professor { Matricula = matricula, UsuarioId = user.Id });
            await _db.SaveChangesAsync();
        }
        else
        {
            // ADMIN
            matricula = "0001";
        }

        return Ok(new AdminCriarUsuarioResultDto(
            user.Id,
            user.Nome,
            user.Email,
            user.Cpf,
            user.Perfil.ToString(),
            matricula
        ));
    }


    [HttpPost("alunos")]
    public async Task<ActionResult<AdminCriacaoResultDto>> CriarAluno([FromBody] AdminCriarAlunoDto dto)
    {
        dto.Nome = dto.Nome.Trim();
        dto.Email = dto.Email.Trim().ToLowerInvariant();

        var emailEmUso = await _db.Usuarios.AsNoTracking()
            .AnyAsync(u => u.Email == dto.Email);
        if (emailEmUso) return BadRequest("Email já cadastrado.");

        // gera próxima matrícula (A000001...)
        var last = await _db.Alunos.AsNoTracking()
            .OrderByDescending(a => a.Id)
            .Select(a => a.Matricula)
            .FirstOrDefaultAsync();

        var nextNum = 1;
        if (!string.IsNullOrWhiteSpace(last) && last.Length >= 7 && int.TryParse(last.Substring(1), out var n))
            nextNum = n + 1;

        var matricula = "A" + nextNum.ToString("D6");



        // cria usuário
        var user = new Usuario
        {
            Nome = dto.Nome,
            Email = dto.Email,
            Perfil = PerfilUsuario.ALUNO,
            Ativo = true,
            SenhaHash = ""
        };
        user.SenhaHash = _hasher.HashPassword(user, dto.SenhaInicial);

        _db.Usuarios.Add(user);
        await _db.SaveChangesAsync();

        // cria aluno vinculado
        var aluno = new Aluno
        {
            Matricula = matricula,
            UsuarioId = user.Id
        };

        _db.Alunos.Add(aluno);
        await _db.SaveChangesAsync();

        return Ok(new AdminCriacaoResultDto(user.Id, matricula, user.Nome));
    }

    [HttpPost("professores")]
    public async Task<ActionResult<AdminCriacaoResultDto>> CriarProfessor([FromBody] AdminCriarProfessorDto dto)
    {
        dto.Nome = dto.Nome.Trim();
        dto.Email = dto.Email.Trim().ToLowerInvariant();

        var emailEmUso = await _db.Usuarios.AsNoTracking()
            .AnyAsync(u => u.Email == dto.Email);
        if (emailEmUso) return BadRequest("Email já cadastrado.");

        var last = await _db.Professores.AsNoTracking()
            .OrderByDescending(p => p.Id)
            .Select(p => p.Matricula)
            .FirstOrDefaultAsync();

        var nextNum = 1;
        if (!string.IsNullOrWhiteSpace(last) && last.Length >= 7 && int.TryParse(last.Substring(1), out var n))
            nextNum = n + 1;

        var matricula = "P" + nextNum.ToString("D6");

        var user = new Usuario
        {
            Nome = dto.Nome,
            Email = dto.Email,
            Perfil = PerfilUsuario.PROFESSOR,
            Ativo = true,
            SenhaHash = ""
        };
        user.SenhaHash = _hasher.HashPassword(user, dto.SenhaInicial);

        _db.Usuarios.Add(user);
        await _db.SaveChangesAsync();

        var prof = new Professor
        {
            Matricula = matricula,
            UsuarioId = user.Id
        };

        _db.Professores.Add(prof);
        await _db.SaveChangesAsync();

        return Ok(new AdminCriacaoResultDto(user.Id, matricula, user.Nome));
    }

    [HttpPatch("usuarios/{id:int}/status")]
    public async Task<IActionResult> AlterarStatusUsuario(int id, [FromBody] AdminUsuarioStatusDto dto)
    {
        var user = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        // opcional: impedir desativar o único ADMIN
        if (user.Perfil == PerfilUsuario.ADMIN && dto.Ativo == false)
            return BadRequest("Não é permitido desativar o administrador.");

        user.Ativo = dto.Ativo;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("usuarios/{id:int}/reset-senha")]
    public async Task<IActionResult> ResetSenha(int id, [FromBody] AdminResetSenhaDto dto)
    {
        var user = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        user.SenhaHash = _hasher.HashPassword(user, dto.NovaSenha);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPut("usuarios/{id:int}")]
    public async Task<IActionResult> AtualizarUsuario(int id, [FromBody] AdminUsuarioUpdateDto dto)
    {
        dto.Nome = dto.Nome.Trim();
        dto.Email = dto.Email.Trim().ToLowerInvariant();

        var user = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        var emailEmUso = await _db.Usuarios.AsNoTracking()
            .AnyAsync(u => u.Id != id && u.Email == dto.Email);
        if (emailEmUso) return BadRequest("Email já cadastrado.");

        user.Nome = dto.Nome;
        user.Email = dto.Email;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("ofertas/{ofertaId:int}/alunos/{alunoId:int}")]
    public async Task<IActionResult> MatricularAlunoEmOferta(int ofertaId, int alunoId)
    {
        var ofertaOk = await _db.OfertaDisciplinas.AsNoTracking()
            .AnyAsync(o => o.Id == ofertaId && o.Ativa);
        if (!ofertaOk) return NotFound("Oferta não encontrada.");

        var alunoOk = await _db.Alunos.AsNoTracking()
            .AnyAsync(a => a.Id == alunoId);
        if (!alunoOk) return NotFound("Aluno não encontrado.");

        var existente = await _db.OfertaAlunos
            .FirstOrDefaultAsync(x => x.OfertaDisciplinaId == ofertaId && x.AlunoId == alunoId);

        if (existente is null)
        {
            _db.OfertaAlunos.Add(new OfertaAluno
            {
                OfertaDisciplinaId = ofertaId,
                AlunoId = alunoId,
                Ativo = true
            });
        }
        else
        {
            existente.Ativo = true;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("ofertas/{ofertaId:int}/alunos/{alunoId:int}")]
    public async Task<IActionResult> DesmatricularAlunoEmOferta(int ofertaId, int alunoId)
    {
        var existente = await _db.OfertaAlunos
            .FirstOrDefaultAsync(x => x.OfertaDisciplinaId == ofertaId && x.AlunoId == alunoId);

        if (existente is null) return NotFound();

        existente.Ativo = false;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("ofertas/{ofertaId:int}/alunos")]
    public async Task<ActionResult<PagedResultDto<AdminPessoaListItemDto>>> ListarAlunosDaOferta(
    int ofertaId,
    [FromQuery] string? search = null,
    [FromQuery] int skip = 0,
    [FromQuery] int limit = 50)
    {
        if (skip < 0) skip = 0;
        if (limit <= 0) limit = 50;
        if (limit > 200) limit = 200;

        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        var ofertaOk = await _db.OfertaDisciplinas.AsNoTracking()
            .AnyAsync(o => o.Id == ofertaId && o.Ativa);
        if (!ofertaOk) return NotFound("Oferta não encontrada.");

        // 1) Pega os alunoIds ativos na oferta
        var alunoIds = await _db.OfertaAlunos.AsNoTracking()
            .Where(x => x.OfertaDisciplinaId == ofertaId && x.Ativo)
            .Select(x => x.AlunoId)
            .Distinct()
            .ToListAsync();

        if (alunoIds.Count == 0)
            return Ok(new PagedResultDto<AdminPessoaListItemDto>(0, new List<AdminPessoaListItemDto>()));

        // 2) Busca alunos (matrícula + usuarioId)
        var alunos = await _db.Alunos.AsNoTracking()
            .Where(a => alunoIds.Contains(a.Id))
            .Select(a => new { a.Id, a.Matricula, a.UsuarioId })
            .ToListAsync();

        // 3) Busca usuários (nome + ativo)
        var userIds = alunos.Select(a => a.UsuarioId).Distinct().ToList();

        var users = await _db.Usuarios.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Nome, u.Cpf, u.Ativo })
            .ToListAsync();

        var userMap = users.ToDictionary(x => x.Id, x => (Nome: x.Nome, Cpf: x.Cpf, Ativo: x.Ativo));

        // 4) Monta lista final
        var itemsAll = alunos
            .Select(a =>
            {
                var info = userMap.TryGetValue(a.UsuarioId, out var u) ? u : (Nome: "", Cpf: "", Ativo: false);
                return new AdminPessoaListItemDto(a.Id, a.Matricula, info.Nome, info.Cpf, info.Ativo);
            })
            .ToList();

        // 5) Filtro (opcional)
        if (search is not null)
        {
            itemsAll = itemsAll
                .Where(x => x.Matricula.Contains(search) || x.Nome.Contains(search))
                .ToList();
        }

        // 6) Total + paginação
        var total = itemsAll.Count;

        var page = itemsAll
            .OrderBy(x => x.Matricula)
            .Skip(skip)
            .Take(limit)
            .ToList();

        return Ok(new PagedResultDto<AdminPessoaListItemDto>(total, page));
    }

    [HttpGet("ofertas")]
    public async Task<ActionResult<PagedResultDto<AdminOfertaListItemDto>>> ListarOfertas(
    [FromQuery] int? turmaId = null,
    [FromQuery] int? disciplinaId = null,
    [FromQuery] int? professorId = null,
    [FromQuery] string? search = null,
    [FromQuery] int skip = 0,
    [FromQuery] int limit = 50)
    {
        if (skip < 0) skip = 0;
        if (limit <= 0) limit = 50;
        if (limit > 200) limit = 200;

        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        var baseQuery = _db.OfertaDisciplinas.AsNoTracking().AsQueryable();

        if (turmaId.HasValue) baseQuery = baseQuery.Where(o => o.TurmaId == turmaId.Value);
        if (disciplinaId.HasValue) baseQuery = baseQuery.Where(o => o.DisciplinaId == disciplinaId.Value);
        if (professorId.HasValue) baseQuery = baseQuery.Where(o => o.ProfessorId == professorId.Value);

        // Tipos compatíveis com seu projeto:
        // TurmaId: int?  | DisciplinaId: int | ProfessorId: int?
        var ofertaRows = await baseQuery
            .Select(o => new
            {
                o.Id,
                TurmaId = (int?)o.TurmaId,
                DisciplinaId = o.DisciplinaId,
                ProfessorId = (int?)o.ProfessorId,
                o.Ativa
            })
            .ToListAsync();

        if (ofertaRows.Count == 0)
            return Ok(new PagedResultDto<AdminOfertaListItemDto>(0, new List<AdminOfertaListItemDto>()));

        var turmaIds = ofertaRows
            .Where(x => x.TurmaId.HasValue)
            .Select(x => x.TurmaId!.Value)
            .Distinct()
            .ToList();

        var discIds = ofertaRows
            .Select(x => x.DisciplinaId)
            .Distinct()
            .ToList();

        var profIds = ofertaRows
            .Where(x => x.ProfessorId.HasValue)
            .Select(x => x.ProfessorId!.Value)
            .Distinct()
            .ToList();

        var turmas = turmaIds.Count == 0
            ? new List<(int Id, string Nome)>()
            : await _db.Turmas.AsNoTracking()
                .Where(t => turmaIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Nome })
                .ToListAsync()
                .ContinueWith(t => t.Result.Select(x => (x.Id, x.Nome)).ToList());

        var turmaMap = turmas.ToDictionary(x => x.Id, x => x.Nome);

        var discs = discIds.Count == 0
            ? new List<(int Id, string Codigo, string Nome)>()
            : await _db.Disciplinas.AsNoTracking()
                .Where(d => discIds.Contains(d.Id))
                .Select(d => new { d.Id, d.Codigo, d.Nome })
                .ToListAsync()
                .ContinueWith(t => t.Result.Select(x => (x.Id, x.Codigo, x.Nome)).ToList());

        var discMap = discs.ToDictionary(x => x.Id, x => (x.Codigo, x.Nome));

        var profs = profIds.Count == 0
            ? new List<(int Id, string Matricula, int UsuarioId)>()
            : await _db.Professores.AsNoTracking()
                .Where(p => profIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Matricula, p.UsuarioId })
                .ToListAsync()
                .ContinueWith(t => t.Result.Select(x => (x.Id, x.Matricula, x.UsuarioId)).ToList());

        var userIds = profs.Select(x => x.UsuarioId).Distinct().ToList();

        var users = userIds.Count == 0
            ? new List<(int Id, string Nome)>()
            : await _db.Usuarios.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Nome })
                .ToListAsync()
                .ContinueWith(t => t.Result.Select(x => (x.Id, x.Nome)).ToList());

        var userMap = users.ToDictionary(x => x.Id, x => x.Nome);

        var profMap = profs.ToDictionary(
            x => x.Id,
            x => (Matricula: x.Matricula, Nome: userMap.TryGetValue(x.UsuarioId, out var n) ? n : "")
        );

        var all = ofertaRows.Select(o =>
        {
            var turmaNome = (o.TurmaId.HasValue && turmaMap.TryGetValue(o.TurmaId.Value, out var tn)) ? tn : "";

            var disc = discMap.TryGetValue(o.DisciplinaId, out var dd)
                ? dd
                : ("", "");

            var prof = (Matricula: "", Nome: "");
            if (o.ProfessorId.HasValue && profMap.TryGetValue(o.ProfessorId.Value, out var pp))
                prof = pp;

            return new AdminOfertaListItemDto(
                o.Id,
                turmaNome,
                disc.Item1,
                disc.Item2,
                prof.Matricula,
                prof.Nome,
                o.Ativa
            );
        }).ToList();

        if (search is not null)
        {
            all = all.Where(x =>
                x.TurmaNome.Contains(search) ||
                x.DisciplinaCodigo.Contains(search) ||
                x.DisciplinaNome.Contains(search) ||
                x.ProfessorMatricula.Contains(search) ||
                x.ProfessorNome.Contains(search)
            ).ToList();
        }

        var total = all.Count;

        var page = all
            .OrderByDescending(x => x.Ativa)
            .ThenBy(x => x.TurmaNome)
            .ThenBy(x => x.DisciplinaCodigo)
            .Skip(skip)
            .Take(limit)
            .ToList();

        return Ok(new PagedResultDto<AdminOfertaListItemDto>(total, page));
    }

    [HttpPut("ofertas/{id:int}")]
    public async Task<IActionResult> AtualizarOferta(int id, [FromBody] AdminOfertaUpdateDto dto)
    {
        var oferta = await _db.OfertaDisciplinas.FirstOrDefaultAsync(o => o.Id == id);
        if (oferta is null) return NotFound("Oferta não encontrada.");

        // Validações mínimas (seguras)
        if (dto.Ano.HasValue && (dto.Ano.Value < 2000 || dto.Ano.Value > 2100))
            return BadRequest("Ano inválido.");

        if (dto.Semestre.HasValue && dto.Semestre.Value is not (1 or 2))
            return BadRequest("Semestre inválido (1 ou 2).");

        if (dto.TotalAulas.HasValue && (dto.TotalAulas.Value < 1 || dto.TotalAulas.Value > 200))
            return BadRequest("TotalAulas inválido (1..200).");

        if (dto.TurmaId.HasValue)
        {
            var turmaExiste = await _db.Turmas.AsNoTracking().AnyAsync(t => t.Id == dto.TurmaId.Value);
            if (!turmaExiste) return BadRequest("TurmaId não existe.");
            oferta.TurmaId = dto.TurmaId.Value;
        }

        if (dto.ProfessorId.HasValue)
        {
            var profExiste = await _db.Professores.AsNoTracking().AnyAsync(p => p.Id == dto.ProfessorId.Value);
            if (!profExiste) return BadRequest("ProfessorId não existe.");
            oferta.ProfessorId = dto.ProfessorId.Value;
        }

        if (dto.Ano.HasValue) oferta.Ano = dto.Ano.Value;
        if (dto.Semestre.HasValue) oferta.Semestre = dto.Semestre.Value;
        if (dto.Periodo.HasValue) oferta.Periodo = dto.Periodo.Value;
        if (dto.TotalAulas.HasValue) oferta.TotalAulas = dto.TotalAulas.Value;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("ofertas")]
    public async Task<IActionResult> CriarOferta([FromBody] AdminCriarOfertaDto dto)
    {
        var turmaOk = await _db.Turmas.AsNoTracking().AnyAsync(t => t.Id == dto.TurmaId);
        if (!turmaOk) return NotFound("Turma não encontrada.");

        var discOk = await _db.Disciplinas.AsNoTracking().AnyAsync(d => d.Id == dto.DisciplinaId);
        if (!discOk) return NotFound("Disciplina não encontrada.");

        var profOk = await _db.Professores.AsNoTracking().AnyAsync(p => p.Id == dto.ProfessorId);
        if (!profOk) return NotFound("Professor não encontrado.");

        // Regra: não criar duplicado para mesma Turma + Disciplina (independente do professor).
        // Se já existir, reativa e troca professor.
        var existente = await _db.OfertaDisciplinas
            .FirstOrDefaultAsync(o => o.TurmaId == dto.TurmaId && o.DisciplinaId == dto.DisciplinaId);

        if (existente is null)
        {
            var nova = new OfertaDisciplina
            {
                TurmaId = dto.TurmaId,
                DisciplinaId = dto.DisciplinaId,
                ProfessorId = dto.ProfessorId,
                Ativa = true
            };

            _db.OfertaDisciplinas.Add(nova);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(CriarOferta), new { id = nova.Id }, new { ofertaId = nova.Id });
        }

        existente.ProfessorId = dto.ProfessorId;
        existente.Ativa = true;
        await _db.SaveChangesAsync();

        return Ok(new { ofertaId = existente.Id, reativada = true });
    }

    [HttpPatch("ofertas/{id:int}/status")]
    public async Task<IActionResult> AlterarStatusOferta(int id, [FromBody] OfertaStatusDto dto)
    {
        var oferta = await _db.OfertaDisciplinas.FirstOrDefaultAsync(o => o.Id == id);
        if (oferta is null) return NotFound("Oferta não encontrada.");

        oferta.Ativa = dto.Ativa;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("turmas/{turmaId:int}/professores/{professorId:int}")]
    public async Task<IActionResult> VincularProfessorNaTurma(int turmaId, int professorId)
    {
        var turmaOk = await _db.Turmas.AsNoTracking().AnyAsync(t => t.Id == turmaId);
        if (!turmaOk) return NotFound("Turma não encontrada.");

        var profOk = await _db.Professores.AsNoTracking().AnyAsync(p => p.Id == professorId);
        if (!profOk) return NotFound("Professor não encontrado.");

        var existente = await _db.TurmaProfessores
            .FirstOrDefaultAsync(x => x.TurmaId == turmaId && x.ProfessorId == professorId);

        if (existente is null)
        {
            _db.TurmaProfessores.Add(new TurmaProfessor
            {
                TurmaId = turmaId,
                ProfessorId = professorId,
                Ativo = true
            });
        }
        else
        {
            existente.Ativo = true;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("turmas/{turmaId:int}/professores/{professorId:int}")]
    public async Task<IActionResult> DesvincularProfessorDaTurma(int turmaId, int professorId)
    {
        var existente = await _db.TurmaProfessores
            .FirstOrDefaultAsync(x => x.TurmaId == turmaId && x.ProfessorId == professorId);

        if (existente is null) return NotFound();

        existente.Ativo = false;
        await _db.SaveChangesAsync();

        return NoContent();
    }


    [HttpPost("turmas/{turmaId:int}/alunos/{alunoId:int}")]
    public async Task<IActionResult> VincularAlunoNaTurma(int turmaId, int alunoId)
    {
        var turmaOk = await _db.Turmas.AsNoTracking().AnyAsync(t => t.Id == turmaId);
        if (!turmaOk) return NotFound("Turma não encontrada.");

        var alunoOk = await _db.Alunos.AsNoTracking().AnyAsync(a => a.Id == alunoId);
        if (!alunoOk) return NotFound("Aluno não encontrado.");

        var existente = await _db.TurmaAlunos
            .FirstOrDefaultAsync(x => x.TurmaId == turmaId && x.AlunoId == alunoId);

        if (existente is null)
        {
            _db.TurmaAlunos.Add(new TurmaAluno
            {
                TurmaId = turmaId,
                AlunoId = alunoId,
                Ativo = true
            });
        }
        else
        {
            existente.Ativo = true;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("turmas/{turmaId:int}/alunos/{alunoId:int}")]
    public async Task<IActionResult> DesvincularAlunoDaTurma(int turmaId, int alunoId)
    {
        var existente = await _db.TurmaAlunos
            .FirstOrDefaultAsync(x => x.TurmaId == turmaId && x.AlunoId == alunoId);

        if (existente is null) return NotFound();

        existente.Ativo = false;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("turmas/{turmaId:int}/alunos")]
    public async Task<ActionResult<PagedResultDto<AdminPessoaListItemDto>>> ListarAlunosDaTurma(
    int turmaId,
    [FromQuery] string? search = null,
    [FromQuery] int skip = 0,
    [FromQuery] int limit = 50)
    {
        if (skip < 0) skip = 0;
        if (limit <= 0) limit = 50;
        if (limit > 200) limit = 200;

        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        var turmaOk = await _db.Turmas.AsNoTracking().AnyAsync(t => t.Id == turmaId);
        if (!turmaOk) return NotFound("Turma não encontrada.");

        var alunoIds = await _db.TurmaAlunos.AsNoTracking()
            .Where(x => x.TurmaId == turmaId && x.Ativo)
            .Select(x => x.AlunoId)
            .Distinct()
            .ToListAsync();

        if (alunoIds.Count == 0)
            return Ok(new PagedResultDto<AdminPessoaListItemDto>(0, new List<AdminPessoaListItemDto>()));

        var alunos = await _db.Alunos.AsNoTracking()
            .Where(a => alunoIds.Contains(a.Id))
            .Select(a => new { a.Id, a.Matricula, a.UsuarioId })
            .ToListAsync();

        var userIds = alunos.Select(a => a.UsuarioId).Distinct().ToList();

        var users = await _db.Usuarios.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Nome, u.Cpf, u.Ativo })
            .ToListAsync();

        var userMap = users.ToDictionary(x => x.Id, x => (Nome: x.Nome, Cpf: x.Cpf, Ativo: x.Ativo));

        var itemsAll = alunos
            .Select(a =>
            {
                var info = userMap.TryGetValue(a.UsuarioId, out var u) ? u : (Nome: "", Cpf: "", Ativo: false);
                return new AdminPessoaListItemDto(a.Id, a.Matricula, info.Nome, info.Cpf, info.Ativo);
            })
            .ToList();

        if (search is not null)
        {
            itemsAll = itemsAll
                .Where(x => x.Matricula.Contains(search) || x.Nome.Contains(search))
                .ToList();
        }

        var total = itemsAll.Count;

        var page = itemsAll
            .OrderBy(x => x.Matricula)
            .Skip(skip)
            .Take(limit)
            .ToList();

        return Ok(new PagedResultDto<AdminPessoaListItemDto>(total, page));
    }

    [HttpGet("turmas/{turmaId:int}/professores")]
    public async Task<ActionResult<PagedResultDto<AdminPessoaListItemDto>>> ListarProfessoresDaTurma(
    int turmaId,
    [FromQuery] string? search = null,
    [FromQuery] int skip = 0,
    [FromQuery] int limit = 50)
    {
        if (skip < 0) skip = 0;
        if (limit <= 0) limit = 50;
        if (limit > 200) limit = 200;

        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        var turmaOk = await _db.Turmas.AsNoTracking().AnyAsync(t => t.Id == turmaId);
        if (!turmaOk) return NotFound("Turma não encontrada.");

        var profIds = await _db.TurmaProfessores.AsNoTracking()
            .Where(x => x.TurmaId == turmaId && x.Ativo)
            .Select(x => x.ProfessorId)
            .Distinct()
            .ToListAsync();

        if (profIds.Count == 0)
            return Ok(new PagedResultDto<AdminPessoaListItemDto>(0, new List<AdminPessoaListItemDto>()));

        var profs = await _db.Professores.AsNoTracking()
            .Where(p => profIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Matricula, p.UsuarioId })
            .ToListAsync();

        var userIds = profs.Select(p => p.UsuarioId).Distinct().ToList();

        var users = await _db.Usuarios.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Nome, u.Cpf, u.Ativo })
            .ToListAsync();

        var userMap = users.ToDictionary(x => x.Id, x => (Nome: x.Nome, Cpf: x.Cpf, Ativo: x.Ativo));

        var itemsAll = profs
            .Select(p =>
            {
                var info = userMap.TryGetValue(p.UsuarioId, out var u) ? u : (Nome: "", Cpf: "", Ativo: false);
                return new AdminPessoaListItemDto(p.Id, p.Matricula, info.Nome, info.Cpf, info.Ativo);
            })
            .ToList();

        if (search is not null)
        {
            itemsAll = itemsAll
                .Where(x => x.Matricula.Contains(search) || x.Nome.Contains(search))
                .ToList();
        }

        var total = itemsAll.Count;

        var page = itemsAll
            .OrderBy(x => x.Matricula)
            .Skip(skip)
            .Take(limit)
            .ToList();

        return Ok(new PagedResultDto<AdminPessoaListItemDto>(total, page));
    }

}

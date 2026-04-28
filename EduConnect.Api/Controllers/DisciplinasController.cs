using EduConnect.Api.Data;
using EduConnect.Api.Domain;
using EduConnect.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduConnect.Api.Controllers;

[Authorize(Roles = "ADMIN")]
[ApiController]
[Route("api/[controller]")]
public class DisciplinasController : ControllerBase
{
    private readonly AppDbContext _db;

    public DisciplinasController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<DisciplinaReadDto>>> GetAll([FromQuery] bool includeInativas = false)
    {
        var query = _db.Disciplinas.AsNoTracking().AsQueryable();

        if (!includeInativas)
            query = query.Where(d => d.Ativa);

        var list = await query
            .OrderBy(d => d.Codigo)
            .Select(d => new DisciplinaReadDto(d.Id, d.Codigo, d.Nome, d.CargaHoraria, d.Ativa))
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DisciplinaReadDto>> GetById(int id)
    {
        var d = await _db.Disciplinas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound();

        return Ok(new DisciplinaReadDto(d.Id, d.Codigo, d.Nome, d.CargaHoraria, d.Ativa));
    }

    [HttpPost]
    public async Task<ActionResult<DisciplinaReadDto>> Create(DisciplinaCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Codigo)) return BadRequest("Código é obrigatório.");
        if (string.IsNullOrWhiteSpace(dto.Nome)) return BadRequest("Nome é obrigatório.");
        if (dto.CargaHoraria <= 0) return BadRequest("CargaHoraria inválida.");

        var codigo = dto.Codigo.Trim().ToUpper();
        var nome = dto.Nome.Trim();

        var existe = await _db.Disciplinas.AnyAsync(d => d.Codigo == codigo);
        if (existe) return BadRequest("Já existe disciplina com esse código.");

        var disciplina = new Disciplina
        {
            Codigo = codigo,
            Nome = nome,
            CargaHoraria = dto.CargaHoraria,
            Ativa = true
        };

        _db.Disciplinas.Add(disciplina);
        await _db.SaveChangesAsync();

        var read = new DisciplinaReadDto(disciplina.Id, disciplina.Codigo, disciplina.Nome, disciplina.CargaHoraria, disciplina.Ativa);
        return CreatedAtAction(nameof(GetById), new { id = disciplina.Id }, read);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, DisciplinaUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Codigo)) return BadRequest("Código é obrigatório.");
        if (string.IsNullOrWhiteSpace(dto.Nome)) return BadRequest("Nome é obrigatório.");
        if (dto.CargaHoraria <= 0) return BadRequest("CargaHoraria inválida.");

        var disciplina = await _db.Disciplinas.FirstOrDefaultAsync(d => d.Id == id);
        if (disciplina is null) return NotFound();

        var codigo = dto.Codigo.Trim().ToUpper();
        var nome = dto.Nome.Trim();

        var codigoEmUso = await _db.Disciplinas.AnyAsync(d => d.Codigo == codigo && d.Id != id);
        if (codigoEmUso) return BadRequest("Já existe disciplina com esse código.");

        disciplina.Codigo = codigo;
        disciplina.Nome = nome;
        disciplina.CargaHoraria = dto.CargaHoraria;
        disciplina.Ativa = dto.Ativa;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var disciplina = await _db.Disciplinas.FirstOrDefaultAsync(d => d.Id == id);
        if (disciplina is null) return NotFound();

        disciplina.Ativa = false;
        await _db.SaveChangesAsync();

        return NoContent();
    }
}

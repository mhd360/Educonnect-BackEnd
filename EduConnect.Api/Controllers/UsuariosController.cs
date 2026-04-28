using EduConnect.Api.Data;
using EduConnect.Api.Domain;
using EduConnect.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduConnect.Api.Controllers;

[Authorize(Roles = "ADMIN")]
[ApiController]
[Route("api/usuarios")]
public class UsuariosController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsuariosController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<UsuarioReadComMatriculaDto>>> GetAll()
    {
        var usuarios = await _db.Usuarios
            .AsNoTracking()
            .Select(u => new UsuarioReadComMatriculaDto(
                u.Id,
                u.Nome,
                u.Email,
                u.Perfil,
                u.Ativo,
                u.Perfil == PerfilUsuario.ALUNO
                    ? _db.Alunos.Where(a => a.UsuarioId == u.Id).Select(a => a.Matricula).FirstOrDefault() ?? ""
                    : u.Perfil == PerfilUsuario.PROFESSOR
                        ? _db.Professores.Where(p => p.UsuarioId == u.Id).Select(p => p.Matricula).FirstOrDefault() ?? ""
                        : "0001"
            ))
            .ToListAsync();

        return Ok(usuarios);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UsuarioReadComMatriculaDto>> GetById(int id)
    {
        var u = await _db.Usuarios.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(u => new UsuarioReadComMatriculaDto(
                u.Id,
                u.Nome,
                u.Email,
                u.Perfil,
                u.Ativo,
                u.Perfil == PerfilUsuario.ALUNO
                    ? _db.Alunos.Where(a => a.UsuarioId == u.Id).Select(a => a.Matricula).FirstOrDefault() ?? ""
                    : u.Perfil == PerfilUsuario.PROFESSOR
                        ? _db.Professores.Where(p => p.UsuarioId == u.Id).Select(p => p.Matricula).FirstOrDefault() ?? ""
                        : "0001"
            ))
            .FirstOrDefaultAsync();

        if (u is null) return NotFound();

        return Ok(u);
    }
}
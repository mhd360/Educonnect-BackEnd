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
[Route("api/[controller]")]
public class MeController : ControllerBase
{
    private readonly AppDbContext _db;

    public MeController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<MeDto>> Get()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = User.FindFirstValue(ClaimTypes.Role);

        if (string.IsNullOrWhiteSpace(userIdStr) || string.IsNullOrWhiteSpace(role))
            return Unauthorized();

        if (!int.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var user = await _db.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null) return Unauthorized();

        string matricula;

        if (role == PerfilUsuario.ADMIN.ToString())
        {
            matricula = "0001";
        }
        else if (role == PerfilUsuario.ALUNO.ToString())
        {
            matricula = await _db.Alunos
                .AsNoTracking()
                .Where(a => a.UsuarioId == userId)
                .Select(a => a.Matricula)
                .FirstOrDefaultAsync() ?? "";
        }
        else if (role == PerfilUsuario.PROFESSOR.ToString())
        {
            matricula = await _db.Professores
                .AsNoTracking()
                .Where(p => p.UsuarioId == userId)
                .Select(p => p.Matricula)
                .FirstOrDefaultAsync() ?? "";
        }
        else
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(matricula))
            return Unauthorized();

        return Ok(new MeDto(
            user.Id,
            user.Nome,
            role,
            matricula
        ));
    }
}

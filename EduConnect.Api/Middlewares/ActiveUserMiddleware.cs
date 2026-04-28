using System.Security.Claims;
using EduConnect.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace EduConnect.Api.Middlewares;

public class ActiveUserMiddleware
{
    private readonly RequestDelegate _next;

    public ActiveUserMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext context, AppDbContext db)
    {
        // Se não está autenticado, segue (endpoints anônimos continuam funcionando)
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        // Tenta obter userId do token (suporta NameIdentifier, "sub", "id")
        var idStr =
            context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            context.User.FindFirstValue("sub") ??
            context.User.FindFirstValue("id");

        if (!int.TryParse(idStr, out var userId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Token inválido.");
            return;
        }

        var ativo = await db.Usuarios.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Ativo)
            .FirstOrDefaultAsync();

        if (!ativo)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Usuário inativo.");
            return;
        }

        await _next(context);
    }
}

using EduConnect.Api.Domain;

namespace EduConnect.Api.DTOs;

public record UsuarioCreateDto(
    string Nome,
    string Email,
    string Senha,
    PerfilUsuario Perfil
);

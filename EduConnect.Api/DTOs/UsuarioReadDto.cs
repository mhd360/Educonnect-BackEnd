using EduConnect.Api.Domain;

namespace EduConnect.Api.DTOs;

public record UsuarioReadDto(
    int Id,
    string Nome,
    string Email,
    PerfilUsuario Perfil,
    bool Ativo
);

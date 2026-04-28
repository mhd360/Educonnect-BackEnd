using EduConnect.Api.Domain;

namespace EduConnect.Api.DTOs;

public record AdminCriarUsuarioDto(
    string Nome,
    string Email,
    string Cpf,
    PerfilUsuario Perfil
);
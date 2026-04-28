using EduConnect.Api.Domain;

namespace EduConnect.Api.DTOs;

public record UsuarioReadComMatriculaDto(
    int Id,
    string Nome,
    string Email,
    PerfilUsuario Perfil,
    bool Ativo,
    string Matricula
);
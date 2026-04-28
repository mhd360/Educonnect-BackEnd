namespace EduConnect.Api.DTOs;

public record AdminCriarUsuarioResultDto(
    int UsuarioId,
    string Nome,
    string Email,
    string Cpf,
    string Perfil,
    string Matricula
);
namespace EduConnect.Api.DTOs;

public record AlunoReadDto(
    int Id,
    string Matricula,
    int UsuarioId,
    string Nome,
    string Email
);

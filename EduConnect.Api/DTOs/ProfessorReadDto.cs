namespace EduConnect.Api.DTOs;

public record ProfessorReadDto(
    int Id,
    string Matricula,
    int UsuarioId,
    string Nome,
    string Email
);

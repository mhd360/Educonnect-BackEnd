namespace EduConnect.Api.DTOs;

public record ProfessorUpdateDto(
    string Nome,
    string Email,
    bool Ativo
);

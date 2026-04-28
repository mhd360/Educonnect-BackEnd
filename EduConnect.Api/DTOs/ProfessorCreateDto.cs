namespace EduConnect.Api.DTOs;

public record ProfessorCreateDto(
    string Nome,
    string Email,
    string Senha,
    string Cpf
);

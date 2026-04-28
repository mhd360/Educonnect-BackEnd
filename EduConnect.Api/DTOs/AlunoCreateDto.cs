namespace EduConnect.Api.DTOs;

public record AlunoCreateDto(
    string Nome,
    string Email,
    string Senha,
    string Cpf
);

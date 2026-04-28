namespace EduConnect.Api.DTOs;

public record AlunoUpdateDto(
    string Nome,
    string Email,
    bool Ativo
);

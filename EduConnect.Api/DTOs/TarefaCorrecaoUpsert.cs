namespace EduConnect.Api.DTOs;

public record TarefaCorrecaoUpsertDto(
    decimal Nota,
    string? Feedback
);

namespace EduConnect.Api.DTOs;

public record TarefaCorrecaoReadDto(
    int Id,
    int TarefaRespostaId,
    decimal Nota,
    string? Feedback,
    DateTime DataCorrecao
);

namespace EduConnect.Api.DTOs;

public record TarefaCorrigidaDto(
    int OfertaId,
    string DisciplinaCodigo,
    string DisciplinaNome,
    int TarefaId,
    string TarefaTitulo,
    decimal Nota,
    string? Feedback,
    DateTime DataCorrecao
);

namespace EduConnect.Api.DTOs;

public record BoletimItemDto(
    int TarefaId,
    string Titulo,
    DateTime? DataEntrega,
    decimal Peso,
    bool Enviada,
    DateTime? DataEnvio,
    bool Corrigida,
    decimal? Nota,
    string? Feedback,
    DateTime? DataCorrecao
);

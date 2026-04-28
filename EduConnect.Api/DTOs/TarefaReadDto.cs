namespace EduConnect.Api.DTOs;

public record TarefaReadDto(
    int Id,
    int OfertaDisciplinaId,
    string Titulo,
    string? Descricao,
    DateTime? DataEntrega,
    decimal Peso,
    bool Ativa
);

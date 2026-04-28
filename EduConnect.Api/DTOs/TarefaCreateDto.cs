namespace EduConnect.Api.DTOs;

public record TarefaCreateDto(
    string Titulo,
    string? Descricao,
    DateTime? DataEntrega,
    decimal Peso
);

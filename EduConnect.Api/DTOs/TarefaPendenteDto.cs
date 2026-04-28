namespace EduConnect.Api.DTOs;

public record TarefaPendenteDto(
    int OfertaId,
    string DisciplinaCodigo,
    string DisciplinaNome,
    int TarefaId,
    string Titulo,
    DateTime? DataEntrega,
    decimal Peso
);

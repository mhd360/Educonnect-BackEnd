namespace EduConnect.Api.DTOs;

public record MinhasNotasItemDto(
    int OfertaId,
    string DisciplinaCodigo,
    string DisciplinaNome,
    int Ano,
    byte Semestre,
    string Periodo,
    decimal? A1,
    decimal? A2,
    decimal? A3
);
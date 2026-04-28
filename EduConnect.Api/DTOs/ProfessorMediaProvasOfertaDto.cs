namespace EduConnect.Api.DTOs;

public record ProfessorMediaProvasOfertaDto(
    int OfertaId,
    string DisciplinaCodigo,
    string DisciplinaNome,
    int Ano,
    byte Semestre,
    string Periodo,
    int TotalAlunosComNota,
    decimal? MediaA1,
    decimal? MediaA2,
    decimal? MediaA3,
    decimal? MediaGeral
);
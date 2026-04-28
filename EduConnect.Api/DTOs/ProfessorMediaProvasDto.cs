namespace EduConnect.Api.DTOs;

public record ProfessorMediaProvasDto(
    int TotalOfertas,
    int TotalAlunosComNota,
    decimal? MediaA1,
    decimal? MediaA2,
    decimal? MediaA3,
    decimal? MediaGeral
);
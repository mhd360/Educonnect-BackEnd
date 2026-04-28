using EduConnect.Api.Domain;

namespace EduConnect.Api.DTOs;

public record AdminOfertaUpdateDto(
    int? TurmaId,
    int? ProfessorId,
    int? Ano,
    byte? Semestre,
    PeriodoTurma? Periodo,
    int? TotalAulas
);
using EduConnect.Api.Domain;

namespace EduConnect.Api.DTOs;

public record OfertaCreateDto(
    int DisciplinaId,
    int ProfessorId,
    int? TurmaId,
    int Ano,
    byte Semestre,
    PeriodoTurma Periodo
);

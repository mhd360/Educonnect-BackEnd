using EduConnect.Api.Domain;

namespace EduConnect.Api.DTOs;

public record TurmaCreateDto(
    int Ano,
    byte Semestre,
    PeriodoTurma Periodo
);

namespace EduConnect.Api.DTOs;

public record OfertaMeDto(
    int OfertaId,
    int Ano,
    byte Semestre,
    string Periodo,
    int DisciplinaId,
    string DisciplinaCodigo,
    string DisciplinaNome,
    int ProfessorId,
    string ProfessorMatricula,
    string ProfessorNome,
    int? TurmaId,
    string? TurmaNome
);

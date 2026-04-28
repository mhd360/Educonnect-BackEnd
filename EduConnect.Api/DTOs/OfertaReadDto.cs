namespace EduConnect.Api.DTOs;

public record OfertaReadDto(
    int Id,
    int Ano,
    byte Semestre,
    string Periodo,
    bool Ativa,
    int DisciplinaId,
    string DisciplinaCodigo,
    string DisciplinaNome,
    int ProfessorId,
    string ProfessorMatricula,
    string ProfessorNome,
    int? TurmaId,
    string? TurmaNome
);

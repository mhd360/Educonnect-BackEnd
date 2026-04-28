namespace EduConnect.Api.DTOs;

public record AdminOfertaResumoDto(
    int OfertaId,
    string DisciplinaCodigo,
    string DisciplinaNome,
    string TurmaNome,
    int Ano,
    int Semestre,
    string Periodo,
    string ProfessorMatricula,
    string ProfessorNome
);

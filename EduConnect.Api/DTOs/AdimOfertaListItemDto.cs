namespace EduConnect.Api.DTOs;

public record AdminOfertaListItemDto(
    int OfertaId,
    string TurmaNome,
    string DisciplinaCodigo,
    string DisciplinaNome,
    string ProfessorMatricula,
    string ProfessorNome,
    bool Ativa
);

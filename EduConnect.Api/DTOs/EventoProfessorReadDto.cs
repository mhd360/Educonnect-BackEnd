namespace EduConnect.Api.DTOs;

public record EventoProfessorReadDto(
    int EventoId,
    int OfertaId,
    string DisciplinaCodigo,
    string DisciplinaNome,
    string Titulo,
    string? Descricao,
    DateOnly Data,
    bool DiaInteiro,
    TimeOnly? HoraInicio,
    TimeOnly? HoraFim,
    bool Ativo
);
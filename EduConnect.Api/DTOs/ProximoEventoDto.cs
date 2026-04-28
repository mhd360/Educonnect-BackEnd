namespace EduConnect.Api.DTOs;

public record ProximoEventoDto(
    int EventoId,
    int OfertaId,
    string DisciplinaCodigo,
    string DisciplinaNome,
    string Titulo,
    DateOnly Data,
    bool DiaInteiro,
    TimeOnly? HoraInicio,
    TimeOnly? HoraFim
);

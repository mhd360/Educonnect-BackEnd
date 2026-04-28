namespace EduConnect.Api.DTOs;

public record EventoCreateDto(
    string Titulo,
    string? Descricao,
    DateOnly Data,
    bool DiaInteiro,
    TimeOnly? HoraInicio,
    TimeOnly? HoraFim
);
namespace EduConnect.Api.DTOs;

public record EventoUpdateDto(
    string Titulo,
    string? Descricao,
    DateOnly Data,
    bool DiaInteiro,
    TimeOnly? HoraInicio,
    TimeOnly? HoraFim,
    bool Ativo
);

namespace EduConnect.Api.DTOs;

public record EventoReadDto(
    int Id,
    int OfertaDisciplinaId,
    string Titulo,
    string? Descricao,
    DateOnly Data,
    bool DiaInteiro,
    TimeOnly? HoraInicio,
    TimeOnly? HoraFim,
    bool Ativo
);

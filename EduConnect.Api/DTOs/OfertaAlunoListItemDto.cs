namespace EduConnect.Api.DTOs;

public record OfertaAlunoListItemDto(
    int Id,
    string Matricula,
    string Nome,
    string Email,
    bool AtivoUsuario,
    bool AtivoVinculo
);

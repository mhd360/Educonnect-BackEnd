namespace EduConnect.Api.DTOs;

public record DisciplinaReadDto(
    int Id,
    string Codigo,
    string Nome,
    int CargaHoraria,
    bool Ativa
);

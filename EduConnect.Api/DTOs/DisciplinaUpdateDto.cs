namespace EduConnect.Api.DTOs;

public record DisciplinaUpdateDto(
    string Codigo,
    string Nome,
    int CargaHoraria,
    bool Ativa
);

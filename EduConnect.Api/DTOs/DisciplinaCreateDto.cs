namespace EduConnect.Api.DTOs;

public record DisciplinaCreateDto(
    string Codigo,
    string Nome,
    int CargaHoraria
);

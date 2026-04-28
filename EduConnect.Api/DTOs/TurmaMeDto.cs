namespace EduConnect.Api.DTOs;

public record TurmaMeDto(
    int Id,
    string Nome,
    int Ano,
    byte Semestre,
    string Periodo
);

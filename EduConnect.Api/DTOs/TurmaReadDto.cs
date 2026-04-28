namespace EduConnect.Api.DTOs;

public record TurmaReadDto(
    int Id,
    string Nome,
    int Ano,
    byte Semestre,
    string Periodo,
    bool Ativa
);

using EduConnect.Api.Domain;

namespace EduConnect.Api.DTOs;

public record BoletimOfertaMeResumoDto(
    int OfertaId,
    string DisciplinaCodigo,
    string DisciplinaNome,
    int Ano,
    byte Semestre,
    string Periodo,

    decimal? MediaPonderada,
    decimal FrequenciaPct,
    StatusDisciplinaBoletim Status,
    decimal? NotaFinal
);

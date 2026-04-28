using EduConnect.Api.Domain;

namespace EduConnect.Api.DTOs;

public record BoletimAlunoResumoDto(
    int AlunoId,
    string Matricula,
    string Nome,

    decimal? MediaPonderada,
    decimal PesoCorrigido,

    int TotalTarefas,
    int TarefasEnviadas,
    int TarefasCorrigidas,

    int TotalAulas,
    int Faltas,
    decimal FrequenciaPct,

    StatusDisciplinaBoletim Status,
    decimal? NotaFinal
);

using EduConnect.Api.Domain;

namespace EduConnect.Api.DTOs;

public record BoletimOfertaDto(
    int OfertaId,
    string DisciplinaCodigo,
    string DisciplinaNome,
    int CargaHoraria,
    int Ano,
    byte Semestre,
    PeriodoTurma Periodo,

    int AlunoId,
    string Matricula,
    string AlunoNome,

    decimal? MediaPonderada,
    int TotalTarefas,
    int TarefasEnviadas,
    int TarefasCorrigidas,
    decimal PesoTotal,
    decimal PesoCorrigido,

    int TotalAulas,
    int Faltas,
    int Presencas,
    decimal FrequenciaPct,

    decimal? NotaExame,
    StatusDisciplinaBoletim Status,
    decimal? NotaFinal,

    List<BoletimItemDto> Itens
);

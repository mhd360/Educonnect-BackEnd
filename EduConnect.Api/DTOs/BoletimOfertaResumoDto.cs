namespace EduConnect.Api.DTOs;

public record BoletimOfertaResumoDto(
    int OfertaId,
    string DisciplinaCodigo,
    string DisciplinaNome,
    int CargaHoraria,
    int Ano,
    int Semestre,
    string Periodo,
    int TotalAlunos,

    List<BoletimAlunoResumoDto> Alunos
);

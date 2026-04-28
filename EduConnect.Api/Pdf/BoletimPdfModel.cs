namespace EduConnect.Api.DTOs;

public record BoletimPdfModel(
    string Nome,
    string Matricula,
    string Turma,
    DateTime GeradoEm,
    List<BoletimPdfLinha> Linhas
);

public record BoletimPdfLinha(
    string Disciplina,
    string CodigoDisciplina,
    int CargaHoraria,
    int Faltas,
    decimal FrequenciaPct,
    decimal? NotaFinal,
    string Status
);

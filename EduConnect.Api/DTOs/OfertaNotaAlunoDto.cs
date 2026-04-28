namespace EduConnect.Api.DTOs;

public record OfertaNotaAlunoDto(
    int OfertaId,
    int AlunoId,
    string Matricula,
    string Nome,
    decimal? A1,
    decimal? A2,
    decimal? A3,
    DateTime? AtualizadoEm
);
namespace EduConnect.Api.DTOs;

public record TarefaCorrecaoProfessorAlunoDto(
    int OfertaId,
    int TarefaId,
    string TarefaTitulo,
    int AlunoId,
    string AlunoNome,
    string AlunoMatricula,
    int RespostaId,
    DateTime DataEnvio,
    int CorrecaoId,
    decimal Nota,
    string? Feedback,
    DateTime DataCorrecao
);
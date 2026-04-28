namespace EduConnect.Api.DTOs;

public record TarefaParaCorrigirDto(
    int OfertaId,
    string DisciplinaCodigo,
    string DisciplinaNome,
    int TarefaId,
    string TarefaTitulo,
    DateTime? DataEntrega,
    int AlunoId,
    string AlunoMatricula,
    string AlunoNome,
    int RespostaId,
    DateTime DataEnvio
);

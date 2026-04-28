namespace EduConnect.Api.DTOs;

public record TarefaRespostaReadDto(
    int Id,
    int TarefaId,
    int AlunoId,
    string AlunoMatricula,
    string AlunoNome,
    string Conteudo,
    DateTime DataEnvio
);

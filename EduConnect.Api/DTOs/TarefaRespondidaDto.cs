namespace EduConnect.Api.DTOs;

public record TarefaRespondidaDto(
    int OfertaId,
    string DisciplinaCodigo,
    string DisciplinaNome,
    int TarefaId,
    string TarefaTitulo,
    int TarefaRespostaId,
    DateTime DataEnvio
);

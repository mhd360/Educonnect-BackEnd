namespace EduConnect.Api.Domain;

public class TarefaResposta
{
    public int Id { get; set; }

    public int TarefaId { get; set; }
    public Tarefa Tarefa { get; set; } = null!;

    public int AlunoId { get; set; }
    public Aluno Aluno { get; set; } = null!;

    public string Conteudo { get; set; } = null!; // texto
    public DateTime DataEnvio { get; set; } = DateTime.UtcNow;

    public bool Ativa { get; set; } = true; // permite reenvio: desativa a antiga e cria nova
}

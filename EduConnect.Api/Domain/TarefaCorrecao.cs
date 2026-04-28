namespace EduConnect.Api.Domain;

public class TarefaCorrecao
{
    public int Id { get; set; }

    public int TarefaRespostaId { get; set; }
    public TarefaResposta TarefaResposta { get; set; } = null!;

    public decimal Nota { get; set; }          // ex.: 0 a 10
    public string? Feedback { get; set; }

    public DateTime DataCorrecao { get; set; } = DateTime.UtcNow;

    public bool Ativa { get; set; } = true;    // se quiser “refazer correção”
}

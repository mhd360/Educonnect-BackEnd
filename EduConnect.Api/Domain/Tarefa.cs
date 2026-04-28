namespace EduConnect.Api.Domain;

public class Tarefa
{
    public int Id { get; set; }

    public int OfertaDisciplinaId { get; set; }
    public OfertaDisciplina OfertaDisciplina { get; set; } = null!;

    public string Titulo { get; set; } = null!;
    public string? Descricao { get; set; }

    public DateTime? DataEntrega { get; set; }

    public decimal Peso { get; set; } = 1m; // ex.: 1, 2, 0.5 etc.
    public bool Ativa { get; set; } = true;

    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
}

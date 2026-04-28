namespace EduConnect.Api.Domain;

public class OfertaNota
{
    public int Id { get; set; }

    public int OfertaDisciplinaId { get; set; }
    public OfertaDisciplina OfertaDisciplina { get; set; } = null!;

    public int AlunoId { get; set; }
    public Aluno Aluno { get; set; } = null!;

    public decimal? A1 { get; set; }
    public decimal? A2 { get; set; }
    public decimal? A3 { get; set; }

    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
}
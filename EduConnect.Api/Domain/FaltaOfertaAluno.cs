namespace EduConnect.Api.Domain;

public class FaltaOfertaAluno
{
    public int Id { get; set; }

    public int OfertaDisciplinaId { get; set; }
    public OfertaDisciplina OfertaDisciplina { get; set; } = null!;

    public int AlunoId { get; set; }
    public Aluno Aluno { get; set; } = null!;

    // 1..TotalAulas
    public int NumeroAula { get; set; }

    public DateTime DataMarcacao { get; set; } = DateTime.UtcNow;

    public bool Ativa { get; set; } = true;
}

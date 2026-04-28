namespace EduConnect.Api.Domain;

public class OfertaAluno
{
    public int Id { get; set; }

    public int OfertaDisciplinaId { get; set; }
    public OfertaDisciplina OfertaDisciplina { get; set; } = null!;

    public int AlunoId { get; set; }
    public Aluno Aluno { get; set; } = null!;

    public bool Ativo { get; set; } = true;
    public DateTime DataVinculo { get; set; } = DateTime.UtcNow;
    public int Faltas { get; set; } = 0;
    public decimal? NotaExame { get; set; }
}

namespace EduConnect.Api.Domain;

public class OfertaAlunoFalta
{
    public int Id { get; set; }

    public int OfertaAlunoId { get; set; }
    public OfertaAluno OfertaAluno { get; set; } = null!;

    public int NumeroAula { get; set; } // 1..TotalAulas

    public DateTime DataMarcacao { get; set; } = DateTime.UtcNow;
}

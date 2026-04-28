namespace EduConnect.Api.Domain;

public class OfertaFalta
{
    public int Id { get; set; }

    public int OfertaDisciplinaId { get; set; }
    public OfertaDisciplina OfertaDisciplina { get; set; } = null!;

    public int AlunoId { get; set; }
    public Aluno Aluno { get; set; } = null!;

    // 1..TotalAulas
    public int AulaNumero { get; set; }

    public DateTime DataLancamento { get; set; } = DateTime.UtcNow;

    // para permitir “desmarcar” sem perder histórico (opcional)
    public bool Ativa { get; set; } = true;
}

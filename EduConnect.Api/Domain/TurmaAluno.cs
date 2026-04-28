namespace EduConnect.Api.Domain;

public class TurmaAluno
{
    public int Id { get; set; }

    public int TurmaId { get; set; }
    public Turma Turma { get; set; } = null!;

    public int AlunoId { get; set; }
    public Aluno Aluno { get; set; } = null!;

    public bool Ativo { get; set; } = true;
    public DateTime DataVinculo { get; set; } = DateTime.UtcNow;
}

namespace EduConnect.Api.Domain;

public class TurmaProfessor
{
    public int Id { get; set; }

    public int TurmaId { get; set; }
    public Turma Turma { get; set; } = null!;

    public int ProfessorId { get; set; }
    public Professor Professor { get; set; } = null!;

    public bool Ativo { get; set; } = true;
    public DateTime DataVinculo { get; set; } = DateTime.UtcNow;
}

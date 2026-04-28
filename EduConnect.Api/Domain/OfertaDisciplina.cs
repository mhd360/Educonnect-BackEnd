namespace EduConnect.Api.Domain;

public class OfertaDisciplina
{
    public int Id { get; set; }

    public int DisciplinaId { get; set; }
    public Disciplina Disciplina { get; set; } = null!;

    public int ProfessorId { get; set; }
    public Professor Professor { get; set; } = null!;

    public int? TurmaId { get; set; }      // opcional
    public Turma? Turma { get; set; }

    public int Ano { get; set; }           // ex: 2026
    public byte Semestre { get; set; }     // 1 ou 2
    public PeriodoTurma Periodo { get; set; }

    public int TotalAulas { get; set; } = 16;

    public bool Ativa { get; set; } = true;
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

}

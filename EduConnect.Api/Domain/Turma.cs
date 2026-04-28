namespace EduConnect.Api.Domain;

public class Turma
{
    public int Id { get; set; }

    public string Nome { get; set; } = null!; // ex: 241-M
    public int Ano { get; set; }              // ex: 2024
    public byte Semestre { get; set; }        // 1 ou 2
    public PeriodoTurma Periodo { get; set; } // Matutino/Vespertino/Noturno

    public bool Ativa { get; set; } = true;
}

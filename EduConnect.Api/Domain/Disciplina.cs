namespace EduConnect.Api.Domain;

public class Disciplina
{
    public int Id { get; set; }

    public string Codigo { get; set; } = null!; // ex: MAT101
    public string Nome { get; set; } = null!;   // ex: Cálculo I

    public int CargaHoraria { get; set; }        // em horas
    public bool Ativa { get; set; } = true;
    public int TotalAulas { get; set; }        // em horas

}

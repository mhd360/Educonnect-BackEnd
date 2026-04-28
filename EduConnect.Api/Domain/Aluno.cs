namespace EduConnect.Api.Domain;

public class Aluno
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public string Matricula { get; set; } = null!;
}

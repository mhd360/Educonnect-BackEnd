namespace EduConnect.Api.Domain;

public class Professor
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public string Matricula { get; set; } = null!;
}

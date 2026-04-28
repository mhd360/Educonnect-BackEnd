namespace EduConnect.Api.Domain;

public class Usuario
{
    public int Id { get; set; }

    public string Nome { get; set; } = null!;
    public string? Cpf { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string SenhaHash { get; set; } = null!;
    public PerfilUsuario Perfil { get; set; }

    public bool Ativo { get; set; } = true;
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

    public Aluno? Aluno { get; set; }
    public Professor? Professor { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace EduConnect.Api.DTOs;

public class AdminCriarAlunoDto
{
    [Required, MaxLength(120)]
    public string Nome { get; set; } = "";

    [Required, EmailAddress, MaxLength(200)]
    public string Email { get; set; } = "";

    [Required, MinLength(6), MaxLength(50)]
    public string SenhaInicial { get; set; } = "Aluno123";
}

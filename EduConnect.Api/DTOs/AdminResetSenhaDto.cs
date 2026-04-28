using System.ComponentModel.DataAnnotations;

namespace EduConnect.Api.DTOs;

public class AdminResetSenhaDto
{
    [Required, MinLength(6), MaxLength(50)]
    public string NovaSenha { get; set; } = "";
}

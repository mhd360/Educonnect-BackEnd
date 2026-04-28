using System.ComponentModel.DataAnnotations;

namespace EduConnect.Api.DTOs;

public class AdminUsuarioUpdateDto
{
    [Required, MaxLength(120)]
    public string Nome { get; set; } = "";

    [Required, EmailAddress, MaxLength(200)]
    public string Email { get; set; } = "";
}

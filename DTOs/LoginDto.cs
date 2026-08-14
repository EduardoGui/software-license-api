using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class LoginDto
{
    [Required(ErrorMessage = "Email é obrigatório.")]
    [EmailAddress(ErrorMessage = "Email inválido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Senha é obrigatória.")]
    public string Senha { get; set; } = string.Empty;
}

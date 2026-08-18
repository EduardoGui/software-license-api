using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class DefinirSenhaDto
{
    [Required(ErrorMessage = "Email é obrigatório.")]
    [EmailAddress(ErrorMessage = "Email inválido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Token é obrigatório.")]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nova senha é obrigatória.")]
    public string NovaSenha { get; set; } = string.Empty;
}

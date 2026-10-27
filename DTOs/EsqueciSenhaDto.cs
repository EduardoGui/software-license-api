using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class EsqueciSenhaDto
{
    [Required(ErrorMessage = "Email é obrigatório.")]
    [EmailAddress(ErrorMessage = "Email inválido.")]
    public string Email { get; set; } = string.Empty;
}

public class AlterarSenhaDto
{
    [Required(ErrorMessage = "Senha atual é obrigatória.")]
    public string SenhaAtual { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nova senha é obrigatória.")]
    public string NovaSenha { get; set; } = string.Empty;
}

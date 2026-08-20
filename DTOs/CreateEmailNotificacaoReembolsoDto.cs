using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateEmailNotificacaoReembolsoDto
{
    [Required(ErrorMessage = "E-mail é obrigatório.")]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tipo de destinatário é obrigatório.")]
    public string TipoDestinatario { get; set; } = string.Empty;

    public bool Ativo { get; set; } = true;
}

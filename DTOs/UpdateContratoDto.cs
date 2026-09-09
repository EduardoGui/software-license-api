using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class UpdateContratoDto
{
    [Required(ErrorMessage = "Objeto é obrigatório.")]
    [MaxLength(500)]
    public string Objeto { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Natureza { get; set; }

    [Required(ErrorMessage = "Status é obrigatório.")]
    public string Status { get; set; } = string.Empty;

    public string? Observacoes { get; set; }

    [MaxLength(150)]
    public string? ContatoAprovacaoNome { get; set; }

    [EmailAddress(ErrorMessage = "E-mail do contato de aprovação inválido.")]
    [MaxLength(200)]
    public string? ContatoAprovacaoEmail { get; set; }
}

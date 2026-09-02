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
}

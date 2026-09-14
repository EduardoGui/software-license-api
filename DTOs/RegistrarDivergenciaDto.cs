using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class RegistrarDivergenciaDto
{
    [Required(ErrorMessage = "Tipo de divergência é obrigatório.")]
    public string TipoDivergencia { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Observacao { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateAjusteManualSaldoFeriasDto
{
    [Required(ErrorMessage = "Quantidade é obrigatória.")]
    public int Quantidade { get; set; }

    [Required(ErrorMessage = "Justificativa é obrigatória.")]
    [MaxLength(1000)]
    public string Observacao { get; set; } = string.Empty;
}

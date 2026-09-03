using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateMedicaoBmDto
{
    [Required(ErrorMessage = "Início do período é obrigatório.")]
    public DateOnly PeriodoInicio { get; set; }

    [Required(ErrorMessage = "Fim do período é obrigatório.")]
    public DateOnly PeriodoFim { get; set; }

    public DateOnly? DataEnvio { get; set; }

    public string? Observacao { get; set; }
}

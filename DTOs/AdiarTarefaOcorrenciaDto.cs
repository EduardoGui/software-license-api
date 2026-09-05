using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class AdiarTarefaOcorrenciaDto
{
    [Required(ErrorMessage = "Nova data é obrigatória.")]
    public DateOnly NovaData { get; set; }

    [MaxLength(500)]
    public string? Observacao { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class EditarTarefaOcorrenciaDto
{
    [Required(ErrorMessage = "Título é obrigatório.")]
    [MaxLength(200)]
    public string Titulo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nova data é obrigatória.")]
    public DateOnly NovaData { get; set; }

    [MaxLength(500)]
    public string? Observacao { get; set; }
}

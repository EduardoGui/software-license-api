using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateTarefaUnicaDto
{
    [Required(ErrorMessage = "Título é obrigatório.")]
    [MaxLength(200)]
    public string Titulo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Data é obrigatória.")]
    public DateOnly Data { get; set; }

    [MaxLength(500)]
    public string? Observacao { get; set; }
}

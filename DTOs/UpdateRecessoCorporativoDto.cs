using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class UpdateRecessoCorporativoDto
{
    [Required(ErrorMessage = "Nome é obrigatório.")]
    [MaxLength(200)]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Data de início é obrigatória.")]
    public DateOnly DataInicio { get; set; }

    [Required(ErrorMessage = "Data de fim é obrigatória.")]
    public DateOnly DataFim { get; set; }

    [Required(ErrorMessage = "Dias a descontar é obrigatório.")]
    [Range(0, 366, ErrorMessage = "Dias a descontar inválido.")]
    public int DiasADescontar { get; set; }
}

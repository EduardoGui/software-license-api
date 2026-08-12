using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class EncerrarMovimentacaoDto
{
    [Required(ErrorMessage = "Data de fim é obrigatória.")]
    public DateOnly DataFim { get; set; }

    public string? Observacao { get; set; }
}

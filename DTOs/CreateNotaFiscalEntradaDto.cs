using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateNotaFiscalEntradaDto
{
    [Required(ErrorMessage = "Número é obrigatório.")]
    [MaxLength(50)]
    public string Numero { get; set; } = string.Empty;

    [Required(ErrorMessage = "Data de entrada é obrigatória.")]
    public DateOnly DataEntrada { get; set; }

    [MaxLength(200)]
    public string? FornecedorNome { get; set; }

    public string? Observacao { get; set; }
}

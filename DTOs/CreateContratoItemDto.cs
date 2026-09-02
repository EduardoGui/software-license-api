using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateContratoItemDto
{
    [MaxLength(50)]
    public string? Codigo { get; set; }

    [Required(ErrorMessage = "Descrição é obrigatória.")]
    [MaxLength(300)]
    public string Descricao { get; set; } = string.Empty;

    [Required(ErrorMessage = "Unidade é obrigatória.")]
    [MaxLength(20)]
    public string Unidade { get; set; } = string.Empty;

    [Range(0.000001, double.MaxValue, ErrorMessage = "Quantidade deve ser maior que zero.")]
    public decimal QuantidadeContratada { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Valor unitário não pode ser negativo.")]
    public decimal ValorUnitario { get; set; }
}

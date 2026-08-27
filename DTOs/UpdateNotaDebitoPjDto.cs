using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class UpdateNotaDebitoPjDto
{
    [Required, MaxLength(100)]
    public string OperadoraSaude { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? NumeroDocumento { get; set; }

    [MaxLength(500)]
    public string? Descricao { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Desconto { get; set; }

    [Range(0, double.MaxValue)]
    public decimal RetencaoTributaria { get; set; }

    public DateOnly? DataVencimento { get; set; }

    [MaxLength(50)]
    public string? FormaPagamento { get; set; }

    [MaxLength(100)]
    public string? CentroCusto { get; set; }

    [MaxLength(100)]
    public string? Area { get; set; }

    [MaxLength(100)]
    public string? ContaContabil { get; set; }

    [MaxLength(100)]
    public string? ProjetoContrato { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateFaturaOperadoraSaudeDto
{
    [Required, MaxLength(100)]
    public string OperadoraSaude { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string NumeroFatura { get; set; } = string.Empty;

    [Required]
    public int Ano { get; set; }

    [Required]
    public int Mes { get; set; }

    public DateOnly? DataEmissao { get; set; }
    public DateOnly? DataVencimento { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ValorTotal { get; set; }

    [MaxLength(500)]
    public string? Observacao { get; set; }
}

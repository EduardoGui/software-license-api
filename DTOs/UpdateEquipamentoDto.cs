using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class UpdateEquipamentoDto
{
    [MaxLength(100)]
    public string? Marca { get; set; }

    [MaxLength(100)]
    public string? Modelo { get; set; }

    [MaxLength(100)]
    public string? NumeroSerie { get; set; }

    [MaxLength(100)]
    public string? Patrimonio { get; set; }

    [MaxLength(200)]
    public string? FornecedorNome { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Valor mensal não pode ser negativo.")]
    public decimal? ValorMensal { get; set; }

    public DateOnly? DataFimContrato { get; set; }

    [Required(ErrorMessage = "Status é obrigatório.")]
    public string Status { get; set; } = string.Empty;

    public string? Observacao { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateNotaFiscalItemDto
{
    [Required(ErrorMessage = "Tipo de equipamento é obrigatório.")]
    public int TipoEquipamentoId { get; set; }

    [MaxLength(300)]
    public string? Descricao { get; set; }

    [Required(ErrorMessage = "Quantidade é obrigatória.")]
    [Range(1, int.MaxValue, ErrorMessage = "Quantidade deve ser maior que zero.")]
    public int Quantidade { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Valor unitário não pode ser negativo.")]
    public decimal? ValorUnitario { get; set; }

    [Required(ErrorMessage = "Origem é obrigatória.")]
    public string Origem { get; set; } = string.Empty;
}

using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateNotaFiscalItemDto
{
    public string? Destino { get; set; }

    public int? TipoEquipamentoId { get; set; }

    public int? TipoPatrimonioId { get; set; }

    public int? LocalId { get; set; }

    [MaxLength(300)]
    public string? Descricao { get; set; }

    [Required(ErrorMessage = "Quantidade é obrigatória.")]
    [Range(1, int.MaxValue, ErrorMessage = "Quantidade deve ser maior que zero.")]
    public int Quantidade { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Valor unitário não pode ser negativo.")]
    public decimal? ValorUnitario { get; set; }

    public string? Origem { get; set; }
}

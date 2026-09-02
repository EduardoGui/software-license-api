using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateAditivoItemDto
{
    public int? ContratoItemId { get; set; }

    [MaxLength(300)]
    public string? DescricaoNovoItem { get; set; }

    [MaxLength(50)]
    public string? CodigoNovoItem { get; set; }

    [MaxLength(20)]
    public string? UnidadeNovoItem { get; set; }

    public decimal DeltaQuantidade { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Valor unitário não pode ser negativo.")]
    public decimal? NovoValorUnitario { get; set; }
}

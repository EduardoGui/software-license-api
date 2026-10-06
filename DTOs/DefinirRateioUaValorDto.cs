using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class ItemRateioUaValorInputDto
{
    [Required(ErrorMessage = "UnidadeOrcamentariaId é obrigatório.")]
    public int UnidadeOrcamentariaId { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Valor deve ser maior que zero.")]
    public decimal Valor { get; set; }
}

public class DefinirRateioUaValorDto
{
    [Required(ErrorMessage = "Itens é obrigatório.")]
    [MinLength(1, ErrorMessage = "Informe ao menos uma UA para o rateio.")]
    public List<ItemRateioUaValorInputDto> Itens { get; set; } = [];
}

public class DespesaAvulsaRateioUaDto
{
    public int UnidadeOrcamentariaId { get; set; }
    public string UnidadeOrcamentariaCodigo { get; set; } = string.Empty;
    public string UnidadeOrcamentariaDescricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
}

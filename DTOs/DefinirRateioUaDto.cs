using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class ItemRateioUaInputDto
{
    [Required(ErrorMessage = "UnidadeOrcamentariaId é obrigatório.")]
    public int UnidadeOrcamentariaId { get; set; }

    [Range(0.000001, double.MaxValue, ErrorMessage = "Quantidade deve ser maior que zero.")]
    public decimal Quantidade { get; set; }
}

public class DefinirRateioUaDto
{
    [Required(ErrorMessage = "Itens é obrigatório.")]
    [MinLength(1, ErrorMessage = "Informe ao menos uma UA para o rateio.")]
    public List<ItemRateioUaInputDto> Itens { get; set; } = [];
}

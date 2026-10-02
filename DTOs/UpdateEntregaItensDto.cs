using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

// Uma linha de item de entrega. Campanha "Item a item": sempre CampanhaEntregaItemId (item do catálogo).
// Campanha "Kit": texto livre (Descricao/Tamanho/Validade) ou, opcionalmente, CampanhaEntregaItemId.
public class ItemEntregaInputDto
{
    public int? CampanhaEntregaItemId { get; set; }

    [MaxLength(200)]
    public string? Descricao { get; set; }

    [MaxLength(30)]
    public string? Tamanho { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Quantidade deve ser maior que zero.")]
    public int Quantidade { get; set; }

    public DateOnly? Validade { get; set; }
}

public class UpdateEntregaItensDto
{
    [Required(ErrorMessage = "Ao menos um item é obrigatório.")]
    [MinLength(1, ErrorMessage = "Ao menos um item é obrigatório.")]
    public List<ItemEntregaInputDto> Itens { get; set; } = [];
}

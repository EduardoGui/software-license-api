using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

// Histórico de "Nova entrada de itens comprados" da campanha.
public class CampanhaEntregaEntradaDto
{
    public int Id { get; set; }
    public int CampanhaEntregaItemId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string? Tamanho { get; set; }
    public DateOnly Data { get; set; }
    public int Quantidade { get; set; }
    public string? Observacao { get; set; }
    public string? RegistradoPorNome { get; set; }
    public DateTime DataCriacao { get; set; }
}

public class CreateCampanhaEntregaEntradaDto
{
    [Required(ErrorMessage = "Informe a data da entrada.")]
    public DateOnly? Data { get; set; }

    // Nº da nota fiscal, pedido, fornecedor...
    [MaxLength(500)]
    public string? Observacao { get; set; }

    [MinLength(1, ErrorMessage = "Informe ao menos um item.")]
    public List<CampanhaEntregaEntradaItemInputDto> Itens { get; set; } = [];
}

public class CampanhaEntregaEntradaItemInputDto
{
    // Item já existente no catálogo da campanha. Se nulo, informe Descricao/Tamanho: casa com um item existente
    // (mesma descrição e tamanho) ou cria o item novo (ex.: um tamanho que ainda não estava na lista).
    public int? CampanhaEntregaItemId { get; set; }

    [MaxLength(200)]
    public string? Descricao { get; set; }

    [MaxLength(30)]
    public string? Tamanho { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "A quantidade da entrada deve ser maior que zero.")]
    public int Quantidade { get; set; }
}

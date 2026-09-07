using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateOrdemCompraDto
{
    [Required(ErrorMessage = "Data é obrigatória.")]
    public DateOnly Data { get; set; }

    [Required(ErrorMessage = "Solicitante é obrigatório.")]
    [MaxLength(150)]
    public string Solicitante { get; set; } = string.Empty;

    [Required(ErrorMessage = "Obra é obrigatória.")]
    public int LocalId { get; set; }

    [Required(ErrorMessage = "Fornecedor é obrigatório.")]
    public int FornecedorId { get; set; }

    [Required(ErrorMessage = "Condição de pagamento é obrigatória.")]
    [MaxLength(200)]
    public string CondicaoPagamento { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? TipoFrete { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Valor do frete não pode ser negativo.")]
    public decimal ValorFrete { get; set; }

    [MaxLength(300)]
    public string? LocalEntrega { get; set; }

    [MaxLength(100)]
    public string? PrazoEntrega { get; set; }

    public string? ObservacoesSolicitante { get; set; }
    public string? ObservacoesFornecedor { get; set; }

    [Required(ErrorMessage = "Ao menos um item é obrigatório.")]
    [MinLength(1, ErrorMessage = "Ao menos um item é obrigatório.")]
    public List<CreateOrdemCompraItemDto> Itens { get; set; } = [];
}

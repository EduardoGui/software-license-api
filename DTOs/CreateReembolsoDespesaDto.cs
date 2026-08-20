using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateReembolsoDespesaDto
{
    [Required(ErrorMessage = "Finalidade é obrigatória.")]
    [MaxLength(300)]
    public string Finalidade { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? FormaPagamento { get; set; } = "PIX";

    public int? LocalId { get; set; }

    [MaxLength(1000)]
    public string? Observacao { get; set; }

    public List<CreateReembolsoDespesaItemDto> Itens { get; set; } = [];
}

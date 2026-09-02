using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateAditivoDto
{
    [Required(ErrorMessage = "Descrição é obrigatória.")]
    [MaxLength(500)]
    public string Descricao { get; set; } = string.Empty;

    [Required(ErrorMessage = "Data de assinatura é obrigatória.")]
    public DateOnly DataAssinatura { get; set; }

    [Required(ErrorMessage = "Data de efeito é obrigatória.")]
    public DateOnly DataEfeito { get; set; }

    public decimal? DeltaValor { get; set; }

    public DateOnly? NovaDataFimVigencia { get; set; }

    [Range(0, 100, ErrorMessage = "Percentual de reajuste deve estar entre 0 e 100.")]
    public decimal? PercentualReajuste { get; set; }

    public string? Observacao { get; set; }

    public List<CreateAditivoItemDto> Itens { get; set; } = [];
}

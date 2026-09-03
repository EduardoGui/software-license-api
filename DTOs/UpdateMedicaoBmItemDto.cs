using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class UpdateMedicaoBmItemDto
{
    [Required(ErrorMessage = "ItemId é obrigatório.")]
    public int ItemId { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Quantidade medida não pode ser negativa.")]
    public decimal QuantidadeMedidaNestaBm { get; set; }

    public DateOnly? InicioEfetivo { get; set; }
    public DateOnly? FimEfetivo { get; set; }

    // Só usado quando o método de pró-rata do contrato é FracaoManual — o usuário informa o
    // percentual diretamente, sem o sistema calcular a partir de dias.
    [Range(0, 100, ErrorMessage = "Percentual de pró-rata deve estar entre 0 e 100.")]
    public decimal? PercentualProRata { get; set; }

    public decimal? AjusteManual { get; set; }
    public string? JustificativaAjuste { get; set; }
}

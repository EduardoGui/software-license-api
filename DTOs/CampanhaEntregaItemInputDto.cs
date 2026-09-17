using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CampanhaEntregaItemInputDto
{
    [Required(ErrorMessage = "Descrição é obrigatória.")]
    [MaxLength(200)]
    public string Descricao { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Tamanho { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Quantidade deve ser maior que zero.")]
    public int Quantidade { get; set; }

    public DateOnly? Validade { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Quantidade disponível não pode ser negativa.")]
    public int? QuantidadeDisponivel { get; set; }
}

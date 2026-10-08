using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CampanhaEntregaItemInputDto
{
    // Preenchido ao editar um item já existente (preserva o vínculo com as entregas); nulo = item novo.
    public int? Id { get; set; }

    [Required(ErrorMessage = "Descrição é obrigatória.")]
    [MaxLength(200)]
    public string Descricao { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Tamanho { get; set; }

    // Quantidade padrão por pessoa (Kit). Zero = o item não vem no kit: a quantidade é preenchida por colaborador.
    [Range(0, int.MaxValue, ErrorMessage = "Quantidade padrão não pode ser negativa.")]
    public int Quantidade { get; set; }

    public DateOnly? Validade { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Quantidade disponível não pode ser negativa.")]
    public int? QuantidadeDisponivel { get; set; }
}

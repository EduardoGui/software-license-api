using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class UpdateMedicaoBmAcertoDto
{
    public int? MedicaoBmItemId { get; set; }

    [Required(ErrorMessage = "Descrição é obrigatória.")]
    [MaxLength(300)]
    public string Descricao { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Unidade { get; set; }

    public decimal? Quantidade { get; set; }
    public decimal? PrecoUnitario { get; set; }
    public decimal PrecoTotal { get; set; }
}

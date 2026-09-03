using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class UpdateMedicaoBmImpostoDto
{
    public int? MedicaoBmItemId { get; set; }

    [Required(ErrorMessage = "Descrição é obrigatória.")]
    [MaxLength(100)]
    public string Descricao { get; set; } = string.Empty;

    [Range(0, 100, ErrorMessage = "Alíquota deve estar entre 0 e 100.")]
    public decimal Aliquota { get; set; }

    public decimal Base { get; set; }
    public decimal ValorTotal { get; set; }
}

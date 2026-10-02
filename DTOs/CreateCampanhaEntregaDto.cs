using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateCampanhaEntregaDto
{
    [Required(ErrorMessage = "Nome é obrigatório.")]
    [MaxLength(200)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Descricao { get; set; }

    // Kit (padrão) ou ItemAItem - não muda depois de criada.
    [MaxLength(20)]
    public string Tipo { get; set; } = "Kit";
}

using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateTipoDespesaDto
{
    [Required(ErrorMessage = "Nome é obrigatório.")]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    public bool Ativo { get; set; } = true;
}

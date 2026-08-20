using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class UpdateLocalDto
{
    [Required(ErrorMessage = "Nome é obrigatório.")]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Endereco { get; set; }

    public bool Ativo { get; set; }
}

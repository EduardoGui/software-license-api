using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class UpdateDependenteDto
{
    [Required(ErrorMessage = "Nome é obrigatório.")]
    [MaxLength(200)]
    public string Nome { get; set; } = string.Empty;

    public bool Ativo { get; set; } = true;
}

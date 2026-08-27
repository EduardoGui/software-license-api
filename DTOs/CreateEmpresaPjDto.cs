using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateEmpresaPjDto
{
    [Required(ErrorMessage = "Razão social é obrigatória.")]
    [MaxLength(200)]
    public string RazaoSocial { get; set; } = string.Empty;

    [Required(ErrorMessage = "CNPJ é obrigatório.")]
    [MaxLength(20)]
    public string Cnpj { get; set; } = string.Empty;

    public bool Ativa { get; set; } = true;
}

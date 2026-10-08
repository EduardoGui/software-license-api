using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class ReverterConfirmacaoDto
{
    [Required(ErrorMessage = "Informe o motivo da reversão.")]
    [MinLength(5, ErrorMessage = "Descreva o motivo da reversão (mínimo de 5 caracteres).")]
    [MaxLength(500)]
    public string Motivo { get; set; } = string.Empty;
}

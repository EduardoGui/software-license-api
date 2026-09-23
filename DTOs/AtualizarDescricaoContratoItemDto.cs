using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class AtualizarDescricaoContratoItemDto
{
    [Required(ErrorMessage = "Descrição é obrigatória.")]
    [MaxLength(300)]
    public string Descricao { get; set; } = string.Empty;
}

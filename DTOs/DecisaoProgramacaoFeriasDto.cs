using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

// Usado tanto para Reprovar quanto para Devolver - nos dois casos a justificativa é obrigatória.
public class DecisaoProgramacaoFeriasDto
{
    [Required(ErrorMessage = "Justificativa é obrigatória.")]
    [MaxLength(1000)]
    public string ObservacaoAprovador { get; set; } = string.Empty;
}

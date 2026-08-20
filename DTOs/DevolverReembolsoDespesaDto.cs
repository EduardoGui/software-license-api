using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class DevolverReembolsoDespesaDto
{
    [Required(ErrorMessage = "Descreva o motivo da devolução para o solicitante.")]
    [MaxLength(1000)]
    public string ObservacaoAprovador { get; set; } = string.Empty;
}

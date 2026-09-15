using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class AtualizarObservacaoTarefaOcorrenciaDto
{
    [MaxLength(500)]
    public string? Observacao { get; set; }
}

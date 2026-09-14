using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateEntregaLoteDto
{
    [Required(ErrorMessage = "Selecione ao menos um colaborador.")]
    [MinLength(1, ErrorMessage = "Selecione ao menos um colaborador.")]
    public List<int> UsuarioIds { get; set; } = [];

    public List<CreateEntregaItemDto> ItensPadrao { get; set; } = [];
}

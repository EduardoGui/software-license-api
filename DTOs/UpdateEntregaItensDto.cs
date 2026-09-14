using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class UpdateEntregaItensDto
{
    [Required(ErrorMessage = "Ao menos um item é obrigatório.")]
    [MinLength(1, ErrorMessage = "Ao menos um item é obrigatório.")]
    public List<CreateEntregaItemDto> Itens { get; set; } = [];
}

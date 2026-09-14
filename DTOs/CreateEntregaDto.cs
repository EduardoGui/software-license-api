using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateEntregaDto
{
    [Required(ErrorMessage = "Colaborador é obrigatório.")]
    public int UsuarioId { get; set; }

    [Required(ErrorMessage = "Ao menos um item é obrigatório.")]
    [MinLength(1, ErrorMessage = "Ao menos um item é obrigatório.")]
    public List<CreateEntregaItemDto> Itens { get; set; } = [];
}

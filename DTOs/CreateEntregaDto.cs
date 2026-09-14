using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateEntregaDto
{
    [Required(ErrorMessage = "Colaborador é obrigatório.")]
    public int UsuarioId { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateSetorAprovadorDto
{
    [Required(ErrorMessage = "Usuário é obrigatório.")]
    public int UsuarioId { get; set; }
}

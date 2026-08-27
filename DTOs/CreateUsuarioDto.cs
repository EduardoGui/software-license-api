using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateUsuarioDto
{
    [Required(ErrorMessage = "Nome é obrigatório.")]
    [MaxLength(200)]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email é obrigatório.")]
    [EmailAddress(ErrorMessage = "Email inválido.")]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Data de início é obrigatória.")]
    public DateOnly DataInicio { get; set; }

    public DateOnly? DataFim { get; set; }

    public string? Observacao { get; set; }

    // Pj | Clt | Estagio (constantes em UsuarioTipo).
    public string? Tipo { get; set; }

    public int? EmpresaPjId { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateMovimentacaoDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Usuário é obrigatório.")]
    public int UsuarioId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Licença é obrigatória.")]
    public int LicencaId { get; set; }

    [Required(ErrorMessage = "Data de início é obrigatória.")]
    public DateOnly DataInicio { get; set; }

    public string? Observacao { get; set; }
}

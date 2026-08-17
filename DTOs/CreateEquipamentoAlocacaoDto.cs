using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateEquipamentoAlocacaoDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Equipamento é obrigatório.")]
    public int EquipamentoId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Usuário é obrigatório.")]
    public int UsuarioId { get; set; }

    [Required(ErrorMessage = "Data de início é obrigatória.")]
    public DateOnly DataInicio { get; set; }

    public string? Observacao { get; set; }
}

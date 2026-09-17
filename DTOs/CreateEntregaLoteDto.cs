using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateEntregaLoteDto
{
    [Required(ErrorMessage = "Selecione ao menos um colaborador.")]
    [MinLength(1, ErrorMessage = "Selecione ao menos um colaborador.")]
    public List<int> UsuarioIds { get; set; } = [];

    [Range(1, int.MaxValue, ErrorMessage = "Quantidade de kits deve ser maior que zero.")]
    public int QuantidadeKits { get; set; } = 1;

    [MaxLength(1000)]
    public string? Observacao { get; set; }
}

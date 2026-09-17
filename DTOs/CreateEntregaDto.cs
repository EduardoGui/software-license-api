using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateEntregaDto
{
    [Required(ErrorMessage = "Colaborador é obrigatório.")]
    public int UsuarioId { get; set; }

    // Quantos kits essa entrega representa (ex.: diretor pegando pra ele + clientes). Padrão 1.
    [Range(1, int.MaxValue, ErrorMessage = "Quantidade de kits deve ser maior que zero.")]
    public int QuantidadeKits { get; set; } = 1;

    [MaxLength(1000)]
    public string? Observacao { get; set; }
}

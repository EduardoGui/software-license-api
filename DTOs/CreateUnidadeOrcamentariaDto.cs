using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateUnidadeOrcamentariaDto
{
    [Required(ErrorMessage = "Setor é obrigatório.")]
    public int SetorId { get; set; }

    [Required(ErrorMessage = "Código é obrigatório.")]
    [MaxLength(30)]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Descrição é obrigatória.")]
    [MaxLength(200)]
    public string Descricao { get; set; } = string.Empty;

    public string? Apropriacao { get; set; }

    public bool Ativa { get; set; } = true;
}

using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class UpdateFeriadoDto
{
    [Required(ErrorMessage = "Data é obrigatória.")]
    public DateOnly Data { get; set; }

    [Required(ErrorMessage = "Descrição é obrigatória.")]
    [MaxLength(150)]
    public string Descricao { get; set; } = string.Empty;

    [Required(ErrorMessage = "Abrangência é obrigatória.")]
    public string Abrangencia { get; set; } = string.Empty;

    [MaxLength(2)]
    public string? Uf { get; set; }

    [MaxLength(150)]
    public string? Municipio { get; set; }

    public bool Ativo { get; set; }
}

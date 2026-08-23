using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class UpdatePatrimonioItemDto
{
    [MaxLength(300)]
    public string? Descricao { get; set; }

    [MaxLength(100)]
    public string? NumeroPatrimonio { get; set; }

    public int? LocalId { get; set; }

    [MaxLength(1000)]
    public string? Observacao { get; set; }
}

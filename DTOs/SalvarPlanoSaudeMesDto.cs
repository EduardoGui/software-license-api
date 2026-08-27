using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class SalvarPlanoSaudeMesDto
{
    [Required]
    public int Ano { get; set; }

    [Required]
    public int Mes { get; set; }

    public List<SalvarPlanoSaudeMesItemDto> Itens { get; set; } = [];
}

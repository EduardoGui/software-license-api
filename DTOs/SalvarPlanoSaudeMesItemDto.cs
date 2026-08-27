using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class SalvarPlanoSaudeMesItemDto
{
    [Required]
    public int UsuarioId { get; set; }
    public int? DependenteId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal ValorMensal { get; set; }

    [Range(0, double.MaxValue)]
    public decimal ValorCoparticipacao { get; set; }
}

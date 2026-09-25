using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CorrigirCompetenciaNotaDebitoPjDto
{
    [Required]
    public int Ano { get; set; }

    [Required]
    public int Mes { get; set; }
}

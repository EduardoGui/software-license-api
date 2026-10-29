using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class SalvarPlanoSaudeMesItemDto
{
    [Required]
    public int UsuarioId { get; set; }
    public int? DependenteId { get; set; }

    // Pode ser negativo: devolução retroativa de mensalidade (ex.: exclusão de beneficiário).
    [Range(-1_000_000, 1_000_000)]
    public decimal ValorMensal { get; set; }

    [Range(0, double.MaxValue)]
    public decimal ValorCoparticipacao { get; set; }
}

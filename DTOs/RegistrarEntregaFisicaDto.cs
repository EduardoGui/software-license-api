using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class RegistrarEntregaFisicaDto
{
    [Required(ErrorMessage = "Data de entrega é obrigatória.")]
    public DateOnly DataEntregaFisica { get; set; }

    [Required(ErrorMessage = "Responsável pela entrega é obrigatório.")]
    public int ResponsavelEntregaId { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class AdicionarParcelaObrigacaoDto
{
    [Range(0.01, double.MaxValue, ErrorMessage = "Valor da parcela deve ser maior que zero.")]
    public decimal Valor { get; set; }
}

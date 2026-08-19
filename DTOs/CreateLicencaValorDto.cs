using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateLicencaValorDto
{
    [Required(ErrorMessage = "Valor é obrigatório.")]
    [Range(0.01, (double)decimal.MaxValue, ErrorMessage = "Valor deve ser maior que zero.")]
    public decimal Valor { get; set; }

    [Required(ErrorMessage = "Periodicidade é obrigatória.")]
    public string Periodicidade { get; set; } = string.Empty;

    [Required(ErrorMessage = "Data de vigência é obrigatória.")]
    public DateOnly DataVigenciaInicio { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateLicencaDto
{
    [Required(ErrorMessage = "Nome é obrigatório.")]
    [MaxLength(200)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Descricao { get; set; }

    [Required(ErrorMessage = "Quantidade total é obrigatória.")]
    [Range(1, int.MaxValue, ErrorMessage = "Quantidade total deve ser maior que zero.")]
    public int QuantidadeTotal { get; set; }

    [Required(ErrorMessage = "Data de início é obrigatória.")]
    public DateOnly DataInicio { get; set; }

    [Required(ErrorMessage = "Data de término previsto é obrigatória.")]
    public DateOnly DataTerminoPrevisto { get; set; }

    [Required(ErrorMessage = "Dias de antecedência para aviso é obrigatório.")]
    [Range(0, int.MaxValue, ErrorMessage = "Dias de antecedência não pode ser negativo.")]
    public int DiasAntecedenciaAviso { get; set; }

    public string? Observacao { get; set; }

    public bool Ativa { get; set; } = true;

    [Required(ErrorMessage = "Valor é obrigatório.")]
    [Range(0.01, (double)decimal.MaxValue, ErrorMessage = "Valor deve ser maior que zero.")]
    public decimal Valor { get; set; }

    [Required(ErrorMessage = "Periodicidade é obrigatória.")]
    public string Periodicidade { get; set; } = string.Empty;

    public int? NotaFiscalEntradaId { get; set; }
}

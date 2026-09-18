using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateProgramacaoFeriasDto
{
    [Required(ErrorMessage = "Data de início é obrigatória.")]
    public DateOnly DataInicio { get; set; }

    [Required(ErrorMessage = "Quantidade de dias é obrigatória.")]
    [Range(1, 30, ErrorMessage = "Quantidade de dias deve estar entre 1 e 30.")]
    public int QuantidadeDias { get; set; }

    public string? Observacao { get; set; }

    public bool AdiantamentoDecimoTerceiro { get; set; }

    public bool AbonoPecuniario { get; set; }

    [Range(0, 30, ErrorMessage = "Dias de abono não pode ser negativo.")]
    public int DiasAbono { get; set; }
}

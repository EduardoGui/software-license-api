using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

// Sem OperadoraSaude/Ano/Mes - definem a identidade da fatura (chave da unicidade e do vínculo com
// as NDs) e não podem mudar depois de criada, mesmo espírito de UsuarioId/Ano/Mes em UpdateNotaDebitoPjDto.
public class UpdateFaturaOperadoraSaudeDto
{
    [Required, MaxLength(50)]
    public string NumeroFatura { get; set; } = string.Empty;

    public DateOnly? DataEmissao { get; set; }
    public DateOnly? DataVencimento { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ValorTotal { get; set; }

    [MaxLength(500)]
    public string? Observacao { get; set; }
}

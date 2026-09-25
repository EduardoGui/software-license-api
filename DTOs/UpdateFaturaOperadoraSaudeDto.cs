using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

// Sem OperadoraSaude - define a identidade/unicidade junto com Ano/Mes e não muda depois de criada.
// Ano/Mes já foram tratados do mesmo jeito (trava total), mas a extração real de faturas mostrou que
// a competência costuma ser descoberta errada na hora do cadastro (a fatura é emitida/vence no mês
// seguinte à competência real) - por isso Ano/Mes viraram editáveis aqui, com verificação de unicidade
// (mesmo espírito da correção pontual feita em NotaDebitoPjService.CorrigirCompetenciaAsync).
public class UpdateFaturaOperadoraSaudeDto
{
    [Required, MaxLength(50)]
    public string NumeroFatura { get; set; } = string.Empty;

    public int? Ano { get; set; }
    public int? Mes { get; set; }

    public DateOnly? DataEmissao { get; set; }
    public DateOnly? DataVencimento { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ValorTotal { get; set; }

    [MaxLength(500)]
    public string? Observacao { get; set; }
}

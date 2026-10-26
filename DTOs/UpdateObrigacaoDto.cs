namespace SoftwareLicense.Api.DTOs;

public class UpdateObrigacaoDto
{
    // Só vale em Ordem de Compra com 2+ parcelas (parcela ainda não paga); nos demais casos o valor previsto vem da origem.
    public decimal? ValorPrevisto { get; set; }
    public DateOnly? DataNf { get; set; }
    public string? NumeroNf { get; set; }
    public decimal? ValorNota { get; set; }
    public DateOnly? Vencimento { get; set; }
    public DateOnly? DataRequisicao { get; set; }
    public DateOnly? DataAssistPgto { get; set; }
    public DateOnly? DataEnvioFinanceiro { get; set; }
    public DateOnly? DataPrevistaPagamento { get; set; }
    public string? Observacoes { get; set; }
}

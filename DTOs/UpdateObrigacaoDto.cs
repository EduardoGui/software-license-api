namespace SoftwareLicense.Api.DTOs;

public class UpdateObrigacaoDto
{
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

namespace SoftwareLicense.Api.DTOs;

public class ContratoSaldoItemDto
{
    public int? ContratoItemId { get; set; }
    public int? AditivoItemId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string Unidade { get; set; } = string.Empty;
    public decimal QuantidadeContratadaAtual { get; set; }
    public decimal QuantidadeJaMedida { get; set; }
    public decimal SaldoQuantidade { get; set; }
    public decimal ValorUnitario { get; set; }
    public decimal ValorContratadoAtual { get; set; }
    public decimal SaldoValor { get; set; }
}

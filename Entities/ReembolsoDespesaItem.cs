namespace SoftwareLicense.Api.Entities;

public class ReembolsoDespesaItem
{
    public int Id { get; set; }
    public int ReembolsoDespesaId { get; set; }
    public ReembolsoDespesa ReembolsoDespesa { get; set; } = null!;
    public DateOnly Data { get; set; }
    public int TipoDespesaId { get; set; }
    public TipoDespesa TipoDespesa { get; set; } = null!;
    public string? Descricao { get; set; }
    public string? NumeroDocumento { get; set; }
    public decimal Valor { get; set; }
    public List<ReembolsoDespesaItemAnexo> Anexos { get; set; } = [];
}

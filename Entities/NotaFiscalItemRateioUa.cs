namespace SoftwareLicense.Api.Entities;

public class NotaFiscalItemRateioUa
{
    public int Id { get; set; }
    public int NotaFiscalItemId { get; set; }
    public NotaFiscalItem NotaFiscalItem { get; set; } = null!;
    public int UnidadeOrcamentariaId { get; set; }
    public UnidadeOrcamentaria UnidadeOrcamentaria { get; set; } = null!;
    public decimal Quantidade { get; set; }
    public DateTime DataCriacao { get; set; }
}

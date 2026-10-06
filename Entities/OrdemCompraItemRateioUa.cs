namespace SoftwareLicense.Api.Entities;

public class OrdemCompraItemRateioUa
{
    public int Id { get; set; }
    public int OrdemCompraItemId { get; set; }
    public OrdemCompraItem OrdemCompraItem { get; set; } = null!;
    public int UnidadeOrcamentariaId { get; set; }
    public UnidadeOrcamentaria UnidadeOrcamentaria { get; set; } = null!;
    public decimal Quantidade { get; set; }
    public DateTime DataCriacao { get; set; }
}

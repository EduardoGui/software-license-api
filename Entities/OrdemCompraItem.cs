namespace SoftwareLicense.Api.Entities;

public class OrdemCompraItem
{
    public int Id { get; set; }
    public int OrdemCompraId { get; set; }
    public OrdemCompra OrdemCompra { get; set; } = null!;
    public string? Codigo { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string Unidade { get; set; } = string.Empty;
    public string? MarcaReferencia { get; set; }
    public decimal Quantidade { get; set; }
    public decimal ValorUnitario { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }

    // Nulo = rateio de UA ainda não definido (bloqueia a emissão da OC).
    public string? MetodoRateioUa { get; set; }
    public List<OrdemCompraItemRateioUa> RateiosUa { get; set; } = [];
}

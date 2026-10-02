namespace SoftwareLicense.Api.Entities;

public class EntregaItem
{
    public int Id { get; set; }
    public int EntregaId { get; set; }
    public Entrega Entrega { get; set; } = null!;

    // Vínculo com o item do catálogo da campanha - é por ele que o saldo de estoque é calculado.
    // Nulo em itens legados que não casaram com nenhum item do catálogo.
    public int? CampanhaEntregaItemId { get; set; }
    public CampanhaEntregaItem? CampanhaEntregaItem { get; set; }

    public string Descricao { get; set; } = string.Empty;
    public string? Tamanho { get; set; }
    public int Quantidade { get; set; }
    public DateOnly? Validade { get; set; }
    public DateTime DataCriacao { get; set; }
}

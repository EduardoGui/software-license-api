namespace SoftwareLicense.Api.Entities;

public class AditivoItem
{
    public int Id { get; set; }
    public int AditivoId { get; set; }
    public Aditivo Aditivo { get; set; } = null!;
    public int? ContratoItemId { get; set; }
    public ContratoItem? ContratoItem { get; set; }

    // Alternativa a ContratoItemId: esta linha soma DeltaQuantidade a um item NOVO criado por um
    // aditivo anterior (que não é um ContratoItem). Mutuamente exclusivo com ContratoItemId e com
    // DescricaoNovoItem; a origem é sempre uma linha que cria item (sem ContratoItemId nem origem).
    public int? AditivoItemOrigemId { get; set; }
    public AditivoItem? AditivoItemOrigem { get; set; }

    public string? DescricaoNovoItem { get; set; }
    public string? CodigoNovoItem { get; set; }
    public string? UnidadeNovoItem { get; set; }
    public decimal DeltaQuantidade { get; set; }
    public decimal? NovoValorUnitario { get; set; }
}

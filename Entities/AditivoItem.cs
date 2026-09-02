namespace SoftwareLicense.Api.Entities;

public class AditivoItem
{
    public int Id { get; set; }
    public int AditivoId { get; set; }
    public Aditivo Aditivo { get; set; } = null!;
    public int? ContratoItemId { get; set; }
    public ContratoItem? ContratoItem { get; set; }
    public string? DescricaoNovoItem { get; set; }
    public string? CodigoNovoItem { get; set; }
    public string? UnidadeNovoItem { get; set; }
    public decimal DeltaQuantidade { get; set; }
    public decimal? NovoValorUnitario { get; set; }
}

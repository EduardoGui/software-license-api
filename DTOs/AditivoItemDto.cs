namespace SoftwareLicense.Api.DTOs;

public class AditivoItemDto
{
    public int Id { get; set; }
    public int? ContratoItemId { get; set; }
    public string? DescricaoContratoItem { get; set; }
    public string? DescricaoNovoItem { get; set; }
    public string? CodigoNovoItem { get; set; }
    public string? UnidadeNovoItem { get; set; }
    public decimal DeltaQuantidade { get; set; }
    public decimal? NovoValorUnitario { get; set; }
}

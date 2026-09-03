namespace SoftwareLicense.Api.DTOs;

public class MedicaoBmAcertoDto
{
    public int Id { get; set; }
    public int? MedicaoBmItemId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string? Unidade { get; set; }
    public decimal? Quantidade { get; set; }
    public decimal? PrecoUnitario { get; set; }
    public decimal PrecoTotal { get; set; }
}

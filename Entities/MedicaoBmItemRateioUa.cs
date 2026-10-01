namespace SoftwareLicense.Api.Entities;

public class MedicaoBmItemRateioUa
{
    public int Id { get; set; }
    public int MedicaoBmItemId { get; set; }
    public MedicaoBmItem MedicaoBmItem { get; set; } = null!;
    public int UnidadeOrcamentariaId { get; set; }
    public UnidadeOrcamentaria UnidadeOrcamentaria { get; set; } = null!;
    public decimal Quantidade { get; set; }
    public DateTime DataCriacao { get; set; }
}

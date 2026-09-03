namespace SoftwareLicense.Api.Entities;

// "Impostos e Retenções" do Boletim de Medição real — retenções tributárias por item (ex.: ISS,
// IRRF, INSS), cada uma com alíquota e base de cálculo próprias.
public class MedicaoBmImposto
{
    public int Id { get; set; }
    public int MedicaoBmId { get; set; }
    public MedicaoBm MedicaoBm { get; set; } = null!;
    public int? MedicaoBmItemId { get; set; }
    public MedicaoBmItem? MedicaoBmItem { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Aliquota { get; set; }
    public decimal Base { get; set; }
    public decimal ValorTotal { get; set; }
}

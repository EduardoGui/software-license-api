namespace SoftwareLicense.Api.Entities;

// "Acertos e Descontos" do Boletim de Medição real — ajustes avulsos aplicados sobre o total
// bruto medido, opcionalmente ligados a um item específico. PrecoTotal pode ser negativo
// (desconto) ou positivo (acréscimo).
public class MedicaoBmAcerto
{
    public int Id { get; set; }
    public int MedicaoBmId { get; set; }
    public MedicaoBm MedicaoBm { get; set; } = null!;
    public int? MedicaoBmItemId { get; set; }
    public MedicaoBmItem? MedicaoBmItem { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string? Unidade { get; set; }
    public decimal? Quantidade { get; set; }
    public decimal? PrecoUnitario { get; set; }
    public decimal PrecoTotal { get; set; }
}

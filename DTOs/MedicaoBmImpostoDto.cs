namespace SoftwareLicense.Api.DTOs;

public class MedicaoBmImpostoDto
{
    public int Id { get; set; }
    public int? MedicaoBmItemId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Aliquota { get; set; }
    public decimal Base { get; set; }
    public decimal ValorTotal { get; set; }
}

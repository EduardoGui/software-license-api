namespace SoftwareLicense.Api.DTOs;

public class OrdemCompraItemDto
{
    public int Id { get; set; }
    public int OrdemCompraId { get; set; }
    public string? Codigo { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string Unidade { get; set; } = string.Empty;
    public string? MarcaReferencia { get; set; }
    public decimal Quantidade { get; set; }
    public decimal ValorUnitario { get; set; }
    public decimal ValorTotal { get; set; }
}

namespace SoftwareLicense.Api.DTOs;

public class ContratoItemDto
{
    public int Id { get; set; }
    public int ContratoId { get; set; }
    public string? Codigo { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string Unidade { get; set; } = string.Empty;
    public decimal QuantidadeContratada { get; set; }
    public decimal ValorUnitario { get; set; }
    public decimal ValorTotal { get; set; }
}

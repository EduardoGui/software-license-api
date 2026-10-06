namespace SoftwareLicense.Api.DTOs;

public class OrdemCompraItemRateioUaDto
{
    public int UnidadeOrcamentariaId { get; set; }
    public string UnidadeOrcamentariaCodigo { get; set; } = string.Empty;
    public string UnidadeOrcamentariaDescricao { get; set; } = string.Empty;
    public decimal Quantidade { get; set; }
}

namespace SoftwareLicense.Api.DTOs;

public class NotaFiscalItemDto
{
    public int Id { get; set; }
    public int NotaFiscalEntradaId { get; set; }
    public int? TipoEquipamentoId { get; set; }
    public string? TipoEquipamentoNome { get; set; }
    public string? Descricao { get; set; }
    public int Quantidade { get; set; }
    public decimal? ValorUnitario { get; set; }
    public string Origem { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }
}

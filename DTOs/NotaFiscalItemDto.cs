namespace SoftwareLicense.Api.DTOs;

public class NotaFiscalItemDto
{
    public int Id { get; set; }
    public int NotaFiscalEntradaId { get; set; }
    public string Destino { get; set; } = string.Empty;
    public int? TipoEquipamentoId { get; set; }
    public string? TipoEquipamentoNome { get; set; }
    public int? TipoPatrimonioId { get; set; }
    public string? TipoPatrimonioNome { get; set; }
    public int? LocalId { get; set; }
    public string? LocalNome { get; set; }
    public string? Descricao { get; set; }
    public int Quantidade { get; set; }
    public decimal? ValorUnitario { get; set; }
    public string Origem { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }
}

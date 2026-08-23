namespace SoftwareLicense.Api.DTOs;

public class PatrimonioItemDto
{
    public int Id { get; set; }
    public int NotaFiscalItemId { get; set; }
    public int? NotaFiscalEntradaId { get; set; }
    public string? NumeroNotaFiscal { get; set; }
    public int TipoPatrimonioId { get; set; }
    public string TipoPatrimonioNome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public string? NumeroPatrimonio { get; set; }
    public int? LocalId { get; set; }
    public string? LocalNome { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Observacao { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}

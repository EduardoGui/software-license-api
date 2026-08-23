namespace SoftwareLicense.Api.Entities;

public class PatrimonioItem
{
    public int Id { get; set; }
    public int NotaFiscalItemId { get; set; }
    public NotaFiscalItem NotaFiscalItem { get; set; } = null!;
    public int TipoPatrimonioId { get; set; }
    public TipoPatrimonio TipoPatrimonio { get; set; } = null!;
    public string? Descricao { get; set; }
    public string? NumeroPatrimonio { get; set; }
    public int? LocalId { get; set; }
    public Local? Local { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Observacao { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}

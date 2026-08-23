namespace SoftwareLicense.Api.DTOs;

public class PatrimonioItemFiltroDto
{
    public int? TipoPatrimonioId { get; set; }
    public int? LocalId { get; set; }
    public string? Status { get; set; }
    public int? NotaFiscalEntradaId { get; set; }
}

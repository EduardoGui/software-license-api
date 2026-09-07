namespace SoftwareLicense.Api.DTOs;

public class DespesaAvulsaFiltroDto
{
    public int? FornecedorId { get; set; }
    public string? Categoria { get; set; }
    public bool? Recorrente { get; set; }
    public DateOnly? DataEmissaoDe { get; set; }
    public DateOnly? DataEmissaoAte { get; set; }
}

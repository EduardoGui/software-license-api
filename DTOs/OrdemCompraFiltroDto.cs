namespace SoftwareLicense.Api.DTOs;

public class OrdemCompraFiltroDto
{
    public int? Numero { get; set; }
    public int? FornecedorId { get; set; }
    public int? LocalId { get; set; }
    public string? Status { get; set; }
}

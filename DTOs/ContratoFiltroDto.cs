namespace SoftwareLicense.Api.DTOs;

public class ContratoFiltroDto
{
    public string? Numero { get; set; }
    public int? FornecedorId { get; set; }
    public string? Status { get; set; }
    public DateOnly? VigenciaFimAte { get; set; }
}

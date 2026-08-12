namespace SoftwareLicense.Api.DTOs;

public class LicencaFiltroDto
{
    public string? Nome { get; set; }
    public string? Status { get; set; }
    public DateOnly? VencimentoAte { get; set; }
}

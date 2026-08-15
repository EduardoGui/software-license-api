namespace SoftwareLicense.Api.DTOs;

public class TimelineFiltroDto
{
    public int? UsuarioId { get; set; }
    public int? LicencaId { get; set; }
    public string? Status { get; set; }
    public DateOnly? DataInicial { get; set; }
    public DateOnly? DataFinal { get; set; }
}

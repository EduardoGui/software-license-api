namespace SoftwareLicense.Api.DTOs;

public class TimelineLicencaDto
{
    public int MovimentacaoId { get; set; }
    public int LicencaId { get; set; }
    public string LicencaNome { get; set; } = string.Empty;
    public DateOnly DataInicio { get; set; }
    public DateOnly? DataFim { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Observacao { get; set; }
}

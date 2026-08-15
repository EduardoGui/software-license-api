namespace SoftwareLicense.Api.DTOs;

public class VencimentoDto
{
    public int LicencaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public DateOnly DataTerminoPrevisto { get; set; }
    public int DiasParaVencer { get; set; }
}

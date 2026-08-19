namespace SoftwareLicense.Api.DTOs;

public class RelatorioMensalCustoLicencasItemDto
{
    public int LicencaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Periodicidade { get; set; }
    public decimal? ValorVigente { get; set; }
    public int DiasAtivos { get; set; }
    public int DiasNoMes { get; set; }
    public decimal ValorNoMes { get; set; }
}

namespace SoftwareLicense.Api.DTOs;

public class RelatorioMensalCustoLicencasItemDto
{
    public int LicencaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int DiasNoMes { get; set; }
    public List<RelatorioMensalCustoLicencasUsuarioDto> Usuarios { get; set; } = [];
    public decimal Subtotal { get; set; }
}

namespace SoftwareLicense.Api.DTOs;

public class RelatorioMensalCustoLicencasDto
{
    public int Ano { get; set; }
    public int Mes { get; set; }
    public List<RelatorioMensalCustoLicencasItemDto> Itens { get; set; } = [];
    public decimal TotalGeral { get; set; }
}

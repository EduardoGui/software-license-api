namespace SoftwareLicense.Api.DTOs;

public class RelatorioMensalCustoLicencasGrupoDto
{
    // "Sem tipo definido" quando as licenças do grupo não têm Licenca.Tipo preenchido.
    public string Tipo { get; set; } = string.Empty;
    public List<RelatorioMensalCustoLicencasItemDto> Licencas { get; set; } = [];
    public decimal Subtotal { get; set; }
}

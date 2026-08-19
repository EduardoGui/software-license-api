using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IRelatorioMensalCustoLicencasService
{
    Task<RelatorioMensalCustoLicencasDto> GerarAsync(RelatorioMensalCustoLicencasFiltroDto filtro);
    byte[] GerarExcel(RelatorioMensalCustoLicencasDto relatorio);
}

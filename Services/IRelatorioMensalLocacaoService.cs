using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IRelatorioMensalLocacaoService
{
    Task<RelatorioMensalLocacaoDto> GerarAsync(RelatorioMensalLocacaoFiltroDto filtro);
    byte[] GerarExcel(RelatorioMensalLocacaoDto relatorio);
}

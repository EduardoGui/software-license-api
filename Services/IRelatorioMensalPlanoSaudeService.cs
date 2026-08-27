using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IRelatorioMensalPlanoSaudeService
{
    Task<RelatorioMensalPlanoSaudeDto> GerarAsync(RelatorioMensalPlanoSaudeFiltroDto filtro);
    byte[] GerarExcel(RelatorioMensalPlanoSaudeDto relatorio);
}

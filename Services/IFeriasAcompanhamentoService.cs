using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IFeriasAcompanhamentoService
{
    Task<FeriasAcompanhamentoDto> ObterAsync(FeriasAcompanhamentoFiltroDto filtro);
    byte[] GerarExcel(FeriasAcompanhamentoDto acompanhamento);
}

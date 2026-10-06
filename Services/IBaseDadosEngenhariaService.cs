using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IBaseDadosEngenhariaService
{
    Task<BaseDadosEngenhariaDto> GerarAsync(BaseDadosEngenhariaFiltroDto filtro);
    byte[] GerarExcel(BaseDadosEngenhariaDto relatorio);
}

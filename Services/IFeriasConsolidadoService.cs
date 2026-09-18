using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IFeriasConsolidadoService
{
    Task<FeriasDashboardDto> ObterDashboardAsync();
    Task<List<FeriasCalendarioUsuarioDto>> ObterCalendarioAsync(FeriasCalendarioFiltroDto filtro);
}

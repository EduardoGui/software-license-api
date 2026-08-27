using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IPlanoSaudeCustoService
{
    Task<PlanoSaudeMesDto> GetMesAsync(PlanoSaudeMesFiltroDto filtro);
    Task<PlanoSaudeMesDto> SalvarMesAsync(SalvarPlanoSaudeMesDto dto);
    Task RemoverAsync(int id);
}

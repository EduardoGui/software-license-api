using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface ITipoPatrimonioService
{
    Task<List<TipoPatrimonioDto>> GetAllAsync(TipoPatrimonioFiltroDto filtro);
    Task<TipoPatrimonioDto> GetByIdAsync(int id);
    Task<TipoPatrimonioDto> CreateAsync(CreateTipoPatrimonioDto dto);
    Task<TipoPatrimonioDto> UpdateAsync(int id, UpdateTipoPatrimonioDto dto);
}

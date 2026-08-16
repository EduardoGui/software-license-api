using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface ITipoEquipamentoService
{
    Task<List<TipoEquipamentoDto>> GetAllAsync(TipoEquipamentoFiltroDto filtro);
    Task<TipoEquipamentoDto> GetByIdAsync(int id);
    Task<TipoEquipamentoDto> CreateAsync(CreateTipoEquipamentoDto dto);
    Task<TipoEquipamentoDto> UpdateAsync(int id, UpdateTipoEquipamentoDto dto);
}

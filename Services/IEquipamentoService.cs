using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IEquipamentoService
{
    Task<List<EquipamentoDto>> GetAllAsync(EquipamentoFiltroDto filtro);
    Task<EquipamentoDto> GetByIdAsync(int id);
    Task<EquipamentoDto> UpdateAsync(int id, UpdateEquipamentoDto dto);
    Task<EquipamentoDto> BaixarAsync(int id);
    Task<InventarioDto> GetInventarioAsync();
}

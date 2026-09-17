using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IUnidadeOrcamentariaService
{
    Task<List<UnidadeOrcamentariaDto>> GetAllAsync(UnidadeOrcamentariaFiltroDto filtro);
    Task<UnidadeOrcamentariaDto> GetByIdAsync(int id);
    Task<UnidadeOrcamentariaDto> CreateAsync(CreateUnidadeOrcamentariaDto dto);
    Task<UnidadeOrcamentariaDto> UpdateAsync(int id, UpdateUnidadeOrcamentariaDto dto);
}

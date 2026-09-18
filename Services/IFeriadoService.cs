using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IFeriadoService
{
    Task<List<FeriadoDto>> GetAllAsync(FeriadoFiltroDto filtro);
    Task<FeriadoDto> GetByIdAsync(int id);
    Task<FeriadoDto> CreateAsync(CreateFeriadoDto dto);
    Task<FeriadoDto> UpdateAsync(int id, UpdateFeriadoDto dto);
}

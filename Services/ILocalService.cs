using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface ILocalService
{
    Task<List<LocalDto>> GetAllAsync(LocalFiltroDto filtro);
    Task<LocalDto> GetByIdAsync(int id);
    Task<LocalDto> CreateAsync(CreateLocalDto dto);
    Task<LocalDto> UpdateAsync(int id, UpdateLocalDto dto);
}

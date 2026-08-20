using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface ISetorService
{
    Task<List<SetorDto>> GetAllAsync(SetorFiltroDto filtro);
    Task<SetorDto> GetByIdAsync(int id);
    Task<SetorDto> CreateAsync(CreateSetorDto dto);
    Task<SetorDto> UpdateAsync(int id, UpdateSetorDto dto);
    Task<SetorDto> AdicionarAprovadorAsync(int setorId, CreateSetorAprovadorDto dto);
    Task<SetorDto> RemoverAprovadorAsync(int setorId, int aprovadorId);
}

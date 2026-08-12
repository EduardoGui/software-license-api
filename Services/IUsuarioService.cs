using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IUsuarioService
{
    Task<List<UsuarioDto>> GetAllAsync(UsuarioFiltroDto filtro);
    Task<UsuarioDto> GetByIdAsync(int id);
    Task<UsuarioDto> CreateAsync(CreateUsuarioDto dto);
    Task<UsuarioDto> UpdateAsync(int id, UpdateUsuarioDto dto);
    Task<UsuarioDto> DesativarAsync(int id, DesativarUsuarioDto dto);
}

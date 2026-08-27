using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IUsuarioService
{
    Task<List<UsuarioDto>> GetAllAsync(UsuarioFiltroDto filtro);
    Task<UsuarioDto> GetByIdAsync(int id);
    Task<UsuarioDto> CreateAsync(CreateUsuarioDto dto);
    Task<UsuarioDto> UpdateAsync(int id, UpdateUsuarioDto dto);
    Task<UsuarioDto> DesativarAsync(int id, DesativarUsuarioDto dto);
    Task<UsuarioDto> AtualizarPerfilAsync(int id, AtualizarPerfilDto dto);
    Task ReenviarConviteAsync(int id);
    Task<UsuarioDto> AdicionarDependenteAsync(int usuarioId, CreateDependenteDto dto);
    Task<UsuarioDto> AtualizarDependenteAsync(int usuarioId, int dependenteId, UpdateDependenteDto dto);
    Task<UsuarioDto> RemoverDependenteAsync(int usuarioId, int dependenteId);
}

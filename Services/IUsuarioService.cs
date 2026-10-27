using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IUsuarioService
{
    Task<List<UsuarioDto>> GetAllAsync(UsuarioFiltroDto filtro);
    Task<UsuarioDto> GetByIdAsync(int id);
    Task<UsuarioDto> CreateAsync(CreateUsuarioDto dto, int? usuarioIdAtor = null);
    Task<UsuarioDto> UpdateAsync(int id, UpdateUsuarioDto dto);
    Task<UsuarioDto> DesativarAsync(int id, DesativarUsuarioDto dto, int? usuarioIdAtor = null);
    Task<UsuarioDto> ReativarAsync(int id, int? usuarioIdAtor = null);
    Task<UsuarioDto> AtualizarPerfilAsync(int id, AtualizarPerfilDto dto);
    Task ReenviarConviteAsync(int id, int? usuarioIdAtor = null);
    Task<bool> CriarContaAcessoAsync(int id, int? usuarioIdAtor = null);
    Task<UsuarioDto> AdicionarDependenteAsync(int usuarioId, CreateDependenteDto dto);
    Task<UsuarioDto> AtualizarDependenteAsync(int usuarioId, int dependenteId, UpdateDependenteDto dto);
    Task<UsuarioDto> RemoverDependenteAsync(int usuarioId, int dependenteId);

    Task<List<AnexoDto>> ListarAnexosAsync(int usuarioId);
    Task<AnexoDto> AdicionarAnexoAsync(int usuarioId, AdicionarAnexoDto dto);
    Task<AnexoArquivoDto> ObterAnexoAsync(int usuarioId, int anexoId);
    Task ExcluirAnexoAsync(int usuarioId, int anexoId);
}

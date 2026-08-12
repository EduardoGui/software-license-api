using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IUsuarioService
{
    Task<List<UsuarioDto>> GetAllAsync();
}

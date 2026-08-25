using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IAuditoriaService
{
    Task RegistrarAsync(int? usuarioId, string entidade, int entidadeId, string acao, string? detalhe = null);
    Task<List<LogAuditoriaDto>> GetAllAsync(LogAuditoriaFiltroDto filtro);
}

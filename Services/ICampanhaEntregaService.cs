using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface ICampanhaEntregaService
{
    Task<List<CampanhaEntregaDto>> GetAllAsync(CampanhaEntregaFiltroDto filtro);
    Task<CampanhaEntregaDto> GetByIdAsync(int id);
    Task<CampanhaEntregaDto> CreateAsync(CreateCampanhaEntregaDto dto);
    Task<CampanhaEntregaDto> UpdateAsync(int id, UpdateCampanhaEntregaDto dto);
    Task DeleteAsync(int id);
    Task<CampanhaEntregaDto> CancelarAsync(int id);
    Task<CampanhaEntregaDto> EncerrarAsync(int id);
    Task<CampanhaEntregaResumoDto> ObterResumoAsync(int campanhaId);

    Task<List<ColaboradorDisponivelDto>> ListarColaboradoresDisponiveisAsync(int campanhaId, ColaboradorDisponivelFiltroDto filtro);

    Task<List<EntregaDto>> ListarEntregasAsync(int campanhaId, EntregaFiltroDto filtro);
    Task<EntregaDto> ObterEntregaAsync(int campanhaId, int entregaId);
    Task<EntregaDto> AdicionarEntregaAsync(int campanhaId, CreateEntregaDto dto);
    Task<List<EntregaDto>> AdicionarEntregasLoteAsync(int campanhaId, CreateEntregaLoteDto dto);
    Task<EntregaDto> AtualizarItensEntregaAsync(int campanhaId, int entregaId, UpdateEntregaItensDto dto);
    Task<EntregaDto> RegistrarEntregaFisicaAsync(int campanhaId, int entregaId, RegistrarEntregaFisicaDto dto);
    Task<EntregaDto> CancelarEntregaAsync(int campanhaId, int entregaId);
    Task<EntregaDto> EnviarEmailAsync(int campanhaId, int entregaId);
    Task<List<EntregaDto>> ReenviarPendentesAsync(int campanhaId);
}

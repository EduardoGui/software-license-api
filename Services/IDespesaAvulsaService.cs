using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IDespesaAvulsaService
{
    Task<List<DespesaAvulsaDto>> GetAllAsync(DespesaAvulsaFiltroDto filtro);
    Task<DespesaAvulsaDto> GetByIdAsync(int id);
    Task<DespesaAvulsaDto> CreateAsync(CreateDespesaAvulsaDto dto);
    Task<DespesaAvulsaDto> UpdateAsync(int id, UpdateDespesaAvulsaDto dto);
    Task DeleteAsync(int id);
    Task<List<AnexoDto>> ListarAnexosAsync(int despesaAvulsaId);
    Task<AnexoDto> AdicionarAnexoAsync(int despesaAvulsaId, AdicionarAnexoDto dto);
    Task<AnexoArquivoDto> ObterAnexoAsync(int despesaAvulsaId, int anexoId);
    Task ExcluirAnexoAsync(int despesaAvulsaId, int anexoId);
}

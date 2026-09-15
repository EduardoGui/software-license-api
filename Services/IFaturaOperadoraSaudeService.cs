using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IFaturaOperadoraSaudeService
{
    Task<List<FaturaOperadoraSaudeDto>> GetAllAsync(FaturaOperadoraSaudeFiltroDto filtro);
    Task<FaturaOperadoraSaudeDto> GetByIdAsync(int id);
    Task<FaturaOperadoraSaudeDto> CreateAsync(CreateFaturaOperadoraSaudeDto dto);
    Task<FaturaOperadoraSaudeDto> UpdateAsync(int id, UpdateFaturaOperadoraSaudeDto dto);
    Task DeleteAsync(int id);

    Task<List<AnexoDto>> ListarAnexosAsync(int faturaId);
    Task<AnexoDto> AdicionarAnexoAsync(int faturaId, AdicionarAnexoDto dto);
    Task<AnexoArquivoDto> ObterAnexoAsync(int faturaId, int anexoId);
    Task ExcluirAnexoAsync(int faturaId, int anexoId);
}

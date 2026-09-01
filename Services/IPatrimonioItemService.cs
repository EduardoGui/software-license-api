using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IPatrimonioItemService
{
    Task<List<PatrimonioItemDto>> GetAllAsync(PatrimonioItemFiltroDto filtro);
    byte[] GerarExcel(List<PatrimonioItemDto> itens);
    Task<PatrimonioItemDto> GetByIdAsync(int id);
    Task<PatrimonioItemDto> UpdateAsync(int id, UpdatePatrimonioItemDto dto);
    Task<PatrimonioItemDto> BaixarAsync(int id);
    Task<List<AnexoDto>> ListarAnexosAsync(int patrimonioItemId);
    Task<AnexoDto> AdicionarAnexoAsync(int patrimonioItemId, AdicionarAnexoDto dto);
    Task<AnexoArquivoDto> ObterAnexoAsync(int patrimonioItemId, int anexoId);
    Task ExcluirAnexoAsync(int patrimonioItemId, int anexoId);
}

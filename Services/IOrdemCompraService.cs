using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IOrdemCompraService
{
    Task<List<OrdemCompraDto>> GetAllAsync(OrdemCompraFiltroDto filtro);
    Task<OrdemCompraDetalheDto> GetByIdAsync(int id);
    Task<OrdemCompraDto> CreateAsync(CreateOrdemCompraDto dto);
    Task<OrdemCompraDto> UpdateAsync(int id, UpdateOrdemCompraDto dto);
    Task<OrdemCompraDto> EmitirAsync(int id);
    Task<OrdemCompraDto> MarcarAssinadaAsync(int id);
    Task<OrdemCompraDto> CancelarAsync(int id);
    Task<byte[]> GerarPdfAsync(int id);
    Task<List<AnexoDto>> ListarAnexosAsync(int ordemCompraId);
    Task<AnexoDto> AdicionarAnexoAsync(int ordemCompraId, AdicionarAnexoDto dto);
    Task<AnexoArquivoDto> ObterAnexoAsync(int ordemCompraId, int anexoId);
    Task ExcluirAnexoAsync(int ordemCompraId, int anexoId);
}

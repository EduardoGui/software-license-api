using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IEquipamentoService
{
    Task<List<EquipamentoDto>> GetAllAsync(EquipamentoFiltroDto filtro);
    Task<EquipamentoDto> GetByIdAsync(int id);
    Task<EquipamentoDto> UpdateAsync(int id, UpdateEquipamentoDto dto);
    Task<EquipamentoDto> BaixarAsync(int id, string? numeroNotaSaida);
    Task<InventarioDto> GetInventarioAsync();
    Task<List<AnexoDto>> ListarAnexosAsync(int equipamentoId);
    Task<AnexoDto> AdicionarAnexoAsync(int equipamentoId, AdicionarAnexoDto dto);
    Task<AnexoArquivoDto> ObterAnexoAsync(int equipamentoId, int anexoId);
    Task ExcluirAnexoAsync(int equipamentoId, int anexoId);
}

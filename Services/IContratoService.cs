using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IContratoService
{
    Task<List<ContratoDto>> GetAllAsync(ContratoFiltroDto filtro);
    Task<ContratoDetalheDto> GetByIdAsync(int id);
    Task<ContratoDto> CreateAsync(CreateContratoDto dto);
    Task<ContratoDto> UpdateAsync(int id, UpdateContratoDto dto);
    Task<ContratoMedicaoConfigDto> AtualizarMedicaoConfigAsync(int id, UpdateContratoMedicaoConfigDto dto);
    Task<ContratoFaturamentoConfigDto> AtualizarFaturamentoConfigAsync(int id, UpdateContratoFaturamentoConfigDto dto);
    Task<List<AnexoDto>> ListarAnexosAsync(int contratoId);
    Task<AnexoDto> AdicionarAnexoAsync(int contratoId, AdicionarAnexoDto dto);
    Task<AnexoArquivoDto> ObterAnexoAsync(int contratoId, int anexoId);
    Task ExcluirAnexoAsync(int contratoId, int anexoId);
    Task<List<AditivoDto>> ListarAditivosAsync(int contratoId);
    Task<AditivoDto> CriarAditivoAsync(int contratoId, CreateAditivoDto dto);
    Task<AditivoDto> FormalizarAditivoAsync(int contratoId, int aditivoId);
}

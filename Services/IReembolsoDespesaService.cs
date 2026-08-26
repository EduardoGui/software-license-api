using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IReembolsoDespesaService
{
    Task<List<ReembolsoDespesaDto>> GetAllAsync(ReembolsoDespesaFiltroDto filtro);
    Task<ReembolsoDespesaDto> GetByIdAsync(int id);
    Task<ReembolsoDespesaDto> CreateAsync(int usuarioId, CreateReembolsoDespesaDto dto);
    Task<ReembolsoDespesaDto> UpdateAsync(int id, UpdateReembolsoDespesaDto dto, int? usuarioIdAtor = null);
    Task ExcluirAsync(int id, int? usuarioIdAtor = null);
    Task<ReembolsoDespesaDto> EnviarAsync(int id, int? usuarioIdAtor = null);
    Task<ReembolsoDespesaDto> AprovarAsync(int id, int aprovadorUsuarioId);
    Task<ReembolsoDespesaDto> DevolverAsync(int id, int aprovadorUsuarioId, DevolverReembolsoDespesaDto dto);
    Task<ReembolsoDespesaDto> ReprovarAsync(int id, int aprovadorUsuarioId, ReprovarReembolsoDespesaDto dto);
    Task<List<ReembolsoDespesaDto>> GetPendentesParaAprovacaoAsync(int aprovadorUsuarioId);
    Task<byte[]> GerarPdfAsync(int id);
    Task<bool> EhAprovadorDoSetorAsync(int usuarioId, int? setorId);
    Task<List<AnexoDto>> ListarAnexosItemAsync(int reembolsoId, int itemId);
    Task<AnexoDto> AdicionarAnexoItemAsync(int reembolsoId, int itemId, AdicionarAnexoDto dto, int? usuarioIdAtor = null);
    Task<AnexoArquivoDto> ObterAnexoItemAsync(int reembolsoId, int itemId, int anexoId);
    Task ExcluirAnexoItemAsync(int reembolsoId, int itemId, int anexoId, int? usuarioIdAtor = null);
}

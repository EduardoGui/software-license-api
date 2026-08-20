using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IReembolsoDespesaService
{
    Task<List<ReembolsoDespesaDto>> GetAllAsync(ReembolsoDespesaFiltroDto filtro);
    Task<ReembolsoDespesaDto> GetByIdAsync(int id);
    Task<ReembolsoDespesaDto> CreateAsync(int usuarioId, CreateReembolsoDespesaDto dto);
    Task<ReembolsoDespesaDto> UpdateAsync(int id, UpdateReembolsoDespesaDto dto);
    Task ExcluirAsync(int id);
    Task<ReembolsoDespesaDto> EnviarAsync(int id);
    Task<ReembolsoDespesaDto> AprovarAsync(int id, int aprovadorUsuarioId);
    Task<ReembolsoDespesaDto> DevolverAsync(int id, int aprovadorUsuarioId, DevolverReembolsoDespesaDto dto);
    Task<ReembolsoDespesaDto> ReprovarAsync(int id, int aprovadorUsuarioId, ReprovarReembolsoDespesaDto dto);
    Task<List<ReembolsoDespesaDto>> GetPendentesParaAprovacaoAsync(int aprovadorUsuarioId);
    Task<byte[]> GerarPdfAsync(int id);
}

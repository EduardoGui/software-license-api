using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface ITipoDespesaService
{
    Task<List<TipoDespesaDto>> GetAllAsync(TipoDespesaFiltroDto filtro);
    Task<TipoDespesaDto> GetByIdAsync(int id);
    Task<TipoDespesaDto> CreateAsync(CreateTipoDespesaDto dto);
    Task<TipoDespesaDto> UpdateAsync(int id, UpdateTipoDespesaDto dto);
}

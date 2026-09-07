using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IObrigacaoService
{
    Task<List<ObrigacaoDto>> GetAllAsync(ObrigacaoFiltroDto filtro);
    Task<ObrigacaoDto> GetByIdAsync(int id);
    Task<ObrigacaoDto> UpdateAsync(int id, UpdateObrigacaoDto dto);
    Task<ObrigacaoDto> MarcarPagaAsync(int id);
    Task<ObrigacaoDto> DesmarcarPagaAsync(int id);
    Task<ObrigacaoDto> CancelarAsync(int id);
}

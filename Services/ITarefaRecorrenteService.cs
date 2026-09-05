using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface ITarefaRecorrenteService
{
    Task<List<TarefaRecorrenteDto>> GetAllAsync(TarefaRecorrenteFiltroDto filtro);
    Task<TarefaRecorrenteDto> GetByIdAsync(int id);
    Task<TarefaRecorrenteDto> CreateAsync(CreateTarefaRecorrenteDto dto);
    Task<TarefaRecorrenteDto> UpdateAsync(int id, UpdateTarefaRecorrenteDto dto);
}

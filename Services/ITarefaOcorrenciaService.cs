using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface ITarefaOcorrenciaService
{
    Task GarantirOcorrenciasDoMesAsync();
    Task<List<TarefaOcorrenciaDto>> ObterAgendaAsync();
    Task<TarefaOcorrenciaDto> ConcluirAsync(int ocorrenciaId);
    Task<TarefaOcorrenciaDto> AdiarAsync(int ocorrenciaId, AdiarTarefaOcorrenciaDto dto);
}

using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface ITarefaOcorrenciaService
{
    Task GarantirOcorrenciasDoMesAsync();
    Task<List<TarefaOcorrenciaDto>> ObterAgendaAsync();
    Task<TarefaOcorrenciaDto> ConcluirAsync(int ocorrenciaId);
    Task<TarefaOcorrenciaDto> EditarAsync(int ocorrenciaId, EditarTarefaOcorrenciaDto dto);
    Task<TarefaOcorrenciaDto> AtualizarObservacaoAsync(int ocorrenciaId, AtualizarObservacaoTarefaOcorrenciaDto dto);
    Task<TarefaOcorrenciaDto> CriarTarefaUnicaAsync(CreateTarefaUnicaDto dto);
}

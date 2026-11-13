using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IRelatorioValidacaoContratosService
{
    // contratoIds vazio/nulo = todos os contratos com tarefa de medição pendente na Agenda.
    Task<RelatorioValidacaoContratosDto> GerarAsync(IReadOnlyCollection<int>? contratoIds);
    byte[] GerarExcel(RelatorioValidacaoContratosDto relatorio);
}

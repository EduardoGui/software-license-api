using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface INotaDebitoPjService
{
    Task<List<NotaDebitoPjDto>> GetAllAsync(NotaDebitoPjFiltroDto filtro);
    Task<NotaDebitoPjDto> GetByIdAsync(int id);
    Task<NotaDebitoPjDto> CreateAsync(CreateNotaDebitoPjDto dto);
    Task<NotaDebitoPjDto> UpdateAsync(int id, UpdateNotaDebitoPjDto dto);
    Task DeleteAsync(int id);
    Task<NotaDebitoPjDto> EnviarAsync(int id);
    Task<NotaDebitoPjDto> PagarAsync(int id, PagarNotaDebitoPjDto dto);
    Task<byte[]> GerarPdfAsync(int id);

    Task<List<AnexoDto>> ListarAnexosAsync(int notaDebitoPjId);
    Task<AnexoDto> AdicionarAnexoAsync(int notaDebitoPjId, AdicionarAnexoDto dto);
    Task<AnexoArquivoDto> ObterAnexoAsync(int notaDebitoPjId, int anexoId);
    Task ExcluirAnexoAsync(int notaDebitoPjId, int anexoId);
}

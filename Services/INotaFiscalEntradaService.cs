using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface INotaFiscalEntradaService
{
    Task<List<NotaFiscalEntradaDto>> GetAllAsync(NotaFiscalEntradaFiltroDto filtro);
    Task<NotaFiscalEntradaDetalheDto> GetByIdAsync(int id);
    Task<NotaFiscalEntradaDto> CreateAsync(CreateNotaFiscalEntradaDto dto);
    Task<NotaFiscalItemDto> AdicionarItemAsync(int notaFiscalEntradaId, CreateNotaFiscalItemDto dto);
}

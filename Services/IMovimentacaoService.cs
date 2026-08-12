using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IMovimentacaoService
{
    Task<PaginaDto<MovimentacaoDto>> GetAllAsync(MovimentacaoFiltroDto filtro);
    Task<MovimentacaoDto> CreateAsync(CreateMovimentacaoDto dto);
    Task<MovimentacaoDto> EncerrarAsync(int id, EncerrarMovimentacaoDto dto);
}

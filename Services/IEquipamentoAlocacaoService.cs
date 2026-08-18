using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IEquipamentoAlocacaoService
{
    Task<PaginaDto<EquipamentoAlocacaoDto>> GetAllAsync(EquipamentoAlocacaoFiltroDto filtro);
    Task<EquipamentoAlocacaoDto> CreateAsync(CreateEquipamentoAlocacaoDto dto);
    Task<EquipamentoAlocacaoDto> EncerrarAsync(int id, EncerrarEquipamentoAlocacaoDto dto);
    Task<EquipamentoAlocacaoDto> EditarEncerradaAsync(int id, EditarEquipamentoAlocacaoEncerradaDto dto);
}

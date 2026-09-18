using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IRecessoCorporativoService
{
    Task<List<RecessoCorporativoDto>> GetAllAsync();
    Task<RecessoCorporativoDto> GetByIdAsync(int id);
    Task<RecessoCorporativoDto> CreateAsync(CreateRecessoCorporativoDto dto, int? usuarioResponsavelId);
    Task<RecessoCorporativoDto> UpdateAsync(int id, UpdateRecessoCorporativoDto dto, int? usuarioResponsavelId);
    Task<List<RecessoSimulacaoLinhaDto>> SimularAsync(int id, SimularRecessoDto dto);
    Task<RecessoCorporativoDto> ConfirmarAsync(int id, SimularRecessoDto dto, int? usuarioResponsavelId);
}

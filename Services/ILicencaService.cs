using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface ILicencaService
{
    Task<List<LicencaDto>> GetAllAsync(LicencaFiltroDto filtro);
    Task<LicencaDto> GetByIdAsync(int id);
    Task<LicencaDto> CreateAsync(CreateLicencaDto dto);
    Task<LicencaDto> UpdateAsync(int id, UpdateLicencaDto dto);
    Task<LicencaDto> DesativarAsync(int id);
    Task<LicencaDto> AdicionarValorAsync(int id, CreateLicencaValorDto dto);
    Task<List<LicencaValorDto>> ListarValoresAsync(int id);
}

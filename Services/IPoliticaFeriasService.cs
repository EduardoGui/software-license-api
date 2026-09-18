using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IPoliticaFeriasService
{
    Task<List<PoliticaFeriasDto>> GetAllAsync();
    Task<PoliticaFeriasDto> GetByIdAsync(int id);
    Task<PoliticaFeriasDto> CreateAsync(CreatePoliticaFeriasDto dto);
    Task<PoliticaFeriasDto> UpdateAsync(int id, UpdatePoliticaFeriasDto dto);
}

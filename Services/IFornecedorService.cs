using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IFornecedorService
{
    Task<List<FornecedorDto>> GetAllAsync(FornecedorFiltroDto filtro);
    Task<FornecedorDto> GetByIdAsync(int id);
    Task<FornecedorDto> CreateAsync(CreateFornecedorDto dto);
    Task<FornecedorDto> UpdateAsync(int id, UpdateFornecedorDto dto);
}

using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IEmpresaPjService
{
    Task<List<EmpresaPjDto>> GetAllAsync(EmpresaPjFiltroDto filtro);
    Task<EmpresaPjDto> GetByIdAsync(int id);
    Task<EmpresaPjDto> CreateAsync(CreateEmpresaPjDto dto);
    Task<EmpresaPjDto> UpdateAsync(int id, UpdateEmpresaPjDto dto);
}

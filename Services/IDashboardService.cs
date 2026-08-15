using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IDashboardService
{
    Task<DashboardDto> ObterAsync();
}

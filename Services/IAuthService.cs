using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface IAuthService
{
    Task<LoginResponseDto?> LoginAsync(LoginDto dto);
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginDto dto)
    {
        var resultado = await _authService.LoginAsync(dto);
        if (resultado is null)
        {
            return Unauthorized(new { message = "Email ou senha inválidos." });
        }

        return Ok(resultado);
    }

    [HttpPost("definir-senha")]
    public async Task<IActionResult> DefinirSenha(DefinirSenhaDto dto)
    {
        await _authService.DefinirSenhaAsync(dto);
        return NoContent();
    }
}

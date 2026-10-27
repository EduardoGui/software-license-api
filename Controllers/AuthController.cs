using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
[EnableRateLimiting("login")]
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

    // Responde sempre 204, exista o e-mail ou não (não revela quem tem conta).
    [HttpPost("esqueci-senha")]
    public async Task<IActionResult> EsqueciSenha(EsqueciSenhaDto dto)
    {
        await _authService.EsqueciSenhaAsync(dto);
        return NoContent();
    }

    [HttpPost("definir-senha")]
    public async Task<IActionResult> DefinirSenha(DefinirSenhaDto dto)
    {
        await _authService.DefinirSenhaAsync(dto);
        return NoContent();
    }
}

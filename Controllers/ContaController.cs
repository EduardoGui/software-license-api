using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

// Ações do usuário logado sobre a própria conta de acesso (qualquer perfil).
[ApiController]
[Route("api/conta")]
[Authorize]
public class ContaController : ControllerBase
{
    private readonly IAuthService _authService;

    public ContaController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("alterar-senha")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> AlterarSenha(AlterarSenhaDto dto)
    {
        var contaId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (contaId is null)
        {
            return Unauthorized();
        }

        await _authService.AlterarSenhaAsync(contaId, dto);
        return NoContent();
    }
}

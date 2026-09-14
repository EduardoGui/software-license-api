using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

// Único ponto de acesso público (sem login) do sistema além do /api/auth - o colaborador confirma
// o recebimento de itens de baixo valor a partir de um link individual enviado por e-mail.
[ApiController]
[Route("api/recebimento")]
[AllowAnonymous]
[EnableRateLimiting("recebimento")]
public class RecebimentoController : ControllerBase
{
    private readonly ICampanhaEntregaService _campanhaEntregaService;

    public RecebimentoController(ICampanhaEntregaService campanhaEntregaService)
    {
        _campanhaEntregaService = campanhaEntregaService;
    }

    [HttpGet("{token}")]
    public async Task<ActionResult<RecebimentoDto>> Obter(string token)
    {
        var resultado = await _campanhaEntregaService.ObterPorTokenAsync(token, ObterIp(), ObterUserAgent());
        return Ok(resultado);
    }

    [HttpPost("{token}/confirmar")]
    public async Task<ActionResult<RecebimentoDto>> Confirmar(string token)
    {
        var resultado = await _campanhaEntregaService.ConfirmarPorTokenAsync(token, ObterIp(), ObterUserAgent());
        return Ok(resultado);
    }

    [HttpPost("{token}/divergencia")]
    public async Task<ActionResult<RecebimentoDto>> Divergencia(string token, RegistrarDivergenciaDto dto)
    {
        var resultado = await _campanhaEntregaService.RegistrarDivergenciaPorTokenAsync(token, dto, ObterIp(), ObterUserAgent());
        return Ok(resultado);
    }

    // A API roda atrás do proxy do Render sem ForwardedHeadersMiddleware configurado, então
    // RemoteIpAddress sozinho refletiria o IP do proxy, não do colaborador - lemos X-Forwarded-For
    // aqui, escopado só a este controller público, sem alterar o comportamento do resto da API.
    private string ObterIp() =>
        Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
        ?? HttpContext.Connection.RemoteIpAddress?.ToString()
        ?? "desconhecido";

    private string? ObterUserAgent() => Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;
}

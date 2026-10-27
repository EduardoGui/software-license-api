using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/acesso")]
[Authorize(Roles = Roles.Administrador)]
public class AcessoController : ControllerBase
{
    private readonly IAcessoService _acessoService;

    public AcessoController(IAcessoService acessoService)
    {
        _acessoService = acessoService;
    }

    private string? ContaIdLogada => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");

    [HttpGet]
    public async Task<ActionResult<List<AcessoContaDto>>> Listar() =>
        Ok(await _acessoService.ListarAsync(ContaIdLogada));

    [HttpPut("contas/{contaId}/papeis")]
    public async Task<IActionResult> DefinirPapeis(string contaId, DefinirPapeisDto dto)
    {
        await _acessoService.DefinirPapeisAsync(contaId, dto.Papeis, ContaIdLogada);
        return NoContent();
    }

    [HttpPatch("contas/{contaId}/bloquear")]
    public async Task<IActionResult> Bloquear(string contaId)
    {
        await _acessoService.BloquearAsync(contaId, ContaIdLogada);
        return NoContent();
    }

    [HttpPatch("contas/{contaId}/desbloquear")]
    public async Task<IActionResult> Desbloquear(string contaId)
    {
        await _acessoService.DesbloquearAsync(contaId);
        return NoContent();
    }
}

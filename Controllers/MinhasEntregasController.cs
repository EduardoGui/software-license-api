using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Extensions;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

// Histórico de entregas do próprio colaborador logado (app mobile) - "o que eu já recebi".
[ApiController]
[Route("api/minhas-entregas")]
[Authorize(Roles = $"{Roles.Administrador},{Roles.Colaborador}")]
public class MinhasEntregasController : ControllerBase
{
    private readonly ICampanhaEntregaService _campanhaEntregaService;

    public MinhasEntregasController(ICampanhaEntregaService campanhaEntregaService)
    {
        _campanhaEntregaService = campanhaEntregaService;
    }

    [HttpGet]
    public async Task<ActionResult<List<MinhaEntregaDto>>> GetAll()
    {
        var usuarioId = User.ObterUsuarioId()
            ?? throw new BusinessRuleException("Esta conta não tem um colaborador vinculado.");

        return Ok(await _campanhaEntregaService.ListarMinhasEntregasAsync(usuarioId));
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/ferias")]
[Authorize(Roles = Roles.Administrador)]
public class FeriasController : ControllerBase
{
    private readonly IFeriasConsolidadoService _feriasConsolidadoService;

    public FeriasController(IFeriasConsolidadoService feriasConsolidadoService)
    {
        _feriasConsolidadoService = feriasConsolidadoService;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<FeriasDashboardDto>> GetDashboard()
    {
        var dashboard = await _feriasConsolidadoService.ObterDashboardAsync();
        return Ok(dashboard);
    }

    [HttpGet("calendario")]
    public async Task<ActionResult<List<FeriasCalendarioUsuarioDto>>> GetCalendario([FromQuery] FeriasCalendarioFiltroDto filtro)
    {
        var calendario = await _feriasConsolidadoService.ObterCalendarioAsync(filtro);
        return Ok(calendario);
    }
}

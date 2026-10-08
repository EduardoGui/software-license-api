using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/ferias")]
[Authorize(Roles = $"{Roles.Administrador},{Roles.Administrativo}")]
public class FeriasController : ControllerBase
{
    private readonly IFeriasConsolidadoService _feriasConsolidadoService;
    private readonly IFeriasAcompanhamentoService _feriasAcompanhamentoService;

    public FeriasController(IFeriasConsolidadoService feriasConsolidadoService, IFeriasAcompanhamentoService feriasAcompanhamentoService)
    {
        _feriasConsolidadoService = feriasConsolidadoService;
        _feriasAcompanhamentoService = feriasAcompanhamentoService;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<FeriasDashboardDto>> GetDashboard()
    {
        var dashboard = await _feriasConsolidadoService.ObterDashboardAsync();
        return Ok(dashboard);
    }

    [HttpGet("acompanhamento")]
    public async Task<ActionResult<FeriasAcompanhamentoDto>> GetAcompanhamento([FromQuery] FeriasAcompanhamentoFiltroDto filtro)
    {
        return Ok(await _feriasAcompanhamentoService.ObterAsync(filtro));
    }

    [HttpGet("acompanhamento/excel")]
    public async Task<IActionResult> GetAcompanhamentoExcel([FromQuery] FeriasAcompanhamentoFiltroDto filtro)
    {
        var acompanhamento = await _feriasAcompanhamentoService.ObterAsync(filtro);
        var arquivo = _feriasAcompanhamentoService.GerarExcel(acompanhamento);
        return File(arquivo, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "acompanhamento-ferias.xlsx");
    }

    [HttpGet("calendario")]
    public async Task<ActionResult<List<FeriasCalendarioUsuarioDto>>> GetCalendario([FromQuery] FeriasCalendarioFiltroDto filtro)
    {
        var calendario = await _feriasConsolidadoService.ObterCalendarioAsync(filtro);
        return Ok(calendario);
    }
}

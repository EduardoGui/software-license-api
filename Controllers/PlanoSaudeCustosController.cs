using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/plano-saude-custos")]
[Authorize(Roles = Roles.Administrador)]
public class PlanoSaudeCustosController : ControllerBase
{
    private readonly IPlanoSaudeCustoService _planoSaudeCustoService;

    public PlanoSaudeCustosController(IPlanoSaudeCustoService planoSaudeCustoService)
    {
        _planoSaudeCustoService = planoSaudeCustoService;
    }

    [HttpGet("mes")]
    public async Task<ActionResult<PlanoSaudeMesDto>> GetMes([FromQuery] PlanoSaudeMesFiltroDto filtro)
    {
        var mes = await _planoSaudeCustoService.GetMesAsync(filtro);
        return Ok(mes);
    }

    [HttpPost("mes")]
    public async Task<ActionResult<PlanoSaudeMesDto>> SalvarMes(SalvarPlanoSaudeMesDto dto)
    {
        var mes = await _planoSaudeCustoService.SalvarMesAsync(dto);
        return Ok(mes);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Remover(int id)
    {
        await _planoSaudeCustoService.RemoverAsync(id);
        return NoContent();
    }
}

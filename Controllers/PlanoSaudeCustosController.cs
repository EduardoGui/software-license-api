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
    private readonly IRelatorioMensalPlanoSaudeService _relatorioMensalPlanoSaudeService;

    public PlanoSaudeCustosController(
        IPlanoSaudeCustoService planoSaudeCustoService, IRelatorioMensalPlanoSaudeService relatorioMensalPlanoSaudeService)
    {
        _planoSaudeCustoService = planoSaudeCustoService;
        _relatorioMensalPlanoSaudeService = relatorioMensalPlanoSaudeService;
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

    [HttpGet("relatorio-mensal")]
    public async Task<IActionResult> RelatorioMensal([FromQuery] RelatorioMensalPlanoSaudeFiltroDto filtro, [FromQuery] string? formato)
    {
        var relatorio = await _relatorioMensalPlanoSaudeService.GerarAsync(filtro);

        if (string.Equals(formato, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            var arquivo = _relatorioMensalPlanoSaudeService.GerarExcel(relatorio);
            var nomeArquivo = $"relatorio-plano-saude-{filtro.Ano:D4}-{filtro.Mes:D2}.xlsx";
            return File(arquivo, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nomeArquivo);
        }

        return Ok(relatorio);
    }
}

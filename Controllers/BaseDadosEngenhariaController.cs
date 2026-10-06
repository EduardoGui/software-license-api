using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/base-dados/engenharia")]
[Authorize(Roles = Roles.Administrador)]
public class BaseDadosEngenhariaController : ControllerBase
{
    private readonly IBaseDadosEngenhariaService _service;

    public BaseDadosEngenhariaController(IBaseDadosEngenhariaService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Gerar([FromQuery] BaseDadosEngenhariaFiltroDto filtro, [FromQuery] string? formato)
    {
        var relatorio = await _service.GerarAsync(filtro);

        if (string.Equals(formato, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            var arquivo = _service.GerarExcel(relatorio);
            var nomeArquivo = $"base-dados-engenharia-{DateTime.UtcNow:yyyyMMdd}.xlsx";
            return File(arquivo, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nomeArquivo);
        }

        return Ok(relatorio);
    }
}

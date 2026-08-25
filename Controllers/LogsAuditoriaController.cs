using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/logs-auditoria")]
[Authorize(Roles = Roles.Administrador)]
public class LogsAuditoriaController : ControllerBase
{
    private readonly IAuditoriaService _auditoriaService;

    public LogsAuditoriaController(IAuditoriaService auditoriaService)
    {
        _auditoriaService = auditoriaService;
    }

    [HttpGet]
    public async Task<ActionResult<List<LogAuditoriaDto>>> GetAll([FromQuery] LogAuditoriaFiltroDto filtro)
    {
        var logs = await _auditoriaService.GetAllAsync(filtro);
        return Ok(logs);
    }
}

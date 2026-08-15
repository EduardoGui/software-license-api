using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/timeline")]
public class TimelineController : ControllerBase
{
    private readonly ITimelineService _timelineService;

    public TimelineController(ITimelineService timelineService)
    {
        _timelineService = timelineService;
    }

    [HttpGet]
    public async Task<ActionResult<List<TimelineUsuarioDto>>> Get([FromQuery] TimelineFiltroDto filtro)
    {
        var timeline = await _timelineService.ObterAsync(filtro);
        return Ok(timeline);
    }
}

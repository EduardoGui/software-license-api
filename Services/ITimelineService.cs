using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public interface ITimelineService
{
    Task<List<TimelineUsuarioDto>> ObterAsync(TimelineFiltroDto filtro);
}

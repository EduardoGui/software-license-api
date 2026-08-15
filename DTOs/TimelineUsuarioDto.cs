namespace SoftwareLicense.Api.DTOs;

public class TimelineUsuarioDto
{
    public int UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = string.Empty;
    public DateOnly DataInicio { get; set; }
    public DateOnly? DataFim { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<TimelineLicencaDto> Licencas { get; set; } = [];
}

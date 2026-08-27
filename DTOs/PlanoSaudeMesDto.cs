namespace SoftwareLicense.Api.DTOs;

public class PlanoSaudeMesDto
{
    public int Ano { get; set; }
    public int Mes { get; set; }
    public List<PlanoSaudeUsuarioMesDto> Usuarios { get; set; } = [];
}

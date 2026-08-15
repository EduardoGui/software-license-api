namespace SoftwareLicense.Api.DTOs;

public class DashboardDto
{
    public int UsuariosAtivos { get; set; }
    public int LicencasAdquiridas { get; set; }
    public int LicencasEmUso { get; set; }
    public int LicencasDisponiveis { get; set; }
    public List<VencimentoDto> ProximosVencimentos { get; set; } = [];
}

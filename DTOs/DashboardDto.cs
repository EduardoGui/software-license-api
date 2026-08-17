namespace SoftwareLicense.Api.DTOs;

public class DashboardDto
{
    public int UsuariosAtivos { get; set; }
    public int LicencasAdquiridas { get; set; }
    public int LicencasEmUso { get; set; }
    public int LicencasDisponiveis { get; set; }
    public List<VencimentoDto> ProximosVencimentos { get; set; } = [];
    public int EquipamentosEmUso { get; set; }
    public int EquipamentosDisponiveis { get; set; }
    public int EquipamentosLocadosAtivos { get; set; }
    public decimal CustoMensalLocacaoAtual { get; set; }
    public List<VencimentoContratoDto> ProximosVencimentosContratos { get; set; } = [];
}

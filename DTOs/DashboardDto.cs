namespace SoftwareLicense.Api.DTOs;

public class DashboardDto
{
    public int UsuariosAtivos { get; set; }
    public int LicencasAdquiridas { get; set; }
    public int LicencasEmUso { get; set; }
    public int LicencasDisponiveis { get; set; }
    public List<VencimentoDto> ProximosVencimentos { get; set; } = [];
    public List<LicencaContagemPorNomeDto> LicencasEmUsoPorNome { get; set; } = [];
    public List<LicencaContagemPorNomeDto> LicencasDisponiveisPorNome { get; set; } = [];
    public List<EquipamentoContagemPorTipoDto> EquipamentosEmUsoPorTipo { get; set; } = [];
    public List<EquipamentoContagemPorTipoDto> EquipamentosDisponiveisPorTipo { get; set; } = [];
    public List<EquipamentoContagemPorTipoDto> EquipamentosLocadosAtivosPorTipo { get; set; } = [];
    public decimal CustoMensalLocacaoAtual { get; set; }
    public List<VencimentoContratoDto> ProximosVencimentosContratos { get; set; } = [];
    public List<AlertaMedicaoDto> AlertasMedicao { get; set; } = [];
}

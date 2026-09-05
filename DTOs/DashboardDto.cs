namespace SoftwareLicense.Api.DTOs;

public class DashboardDto
{
    public int UsuariosAtivos { get; set; }
    public int LicencasDisponiveis { get; set; }
    public List<LicencaContagemPorNomeDto> LicencasEmUsoPorNome { get; set; } = [];
    public List<LicencaContagemPorNomeDto> LicencasDisponiveisPorNome { get; set; } = [];
    public List<EquipamentoContagemPorTipoDto> EquipamentosEmUsoPorTipo { get; set; } = [];
    public List<EquipamentoContagemPorTipoDto> EquipamentosDisponiveisPorTipo { get; set; } = [];
    public List<EquipamentoContagemPorTipoDto> EquipamentosLocadosAtivosPorTipo { get; set; } = [];
    public decimal CustoMensalLocacaoAtual { get; set; }

    // Substitui as antigas listas separadas (ProximosVencimentos, ProximosVencimentosContratos,
    // AlertasMedicao, TarefasPendentes) — tudo que precisa de atenção, junto, ordenado por urgência.
    public List<PendenciaDto> Pendencias { get; set; } = [];
}

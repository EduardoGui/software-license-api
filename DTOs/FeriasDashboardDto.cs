namespace SoftwareLicense.Api.DTOs;

public class FeriasDashboardDto
{
    public int ColaboradoresPj { get; set; }
    public int ColaboradoresSemPeriodoGerado { get; set; }
    public int SaldoTotalDisponivel { get; set; }
    public int ProgramacoesPendentesAprovacao { get; set; }
    public int ProgramacoesEmGozoHoje { get; set; }
    public int RecessosConfirmadosAnoAtual { get; set; }
    public List<ConcessivoVencendoDto> ConcessivosProximosDoVencimento { get; set; } = [];
    public List<ProgramacaoFeriasDto> FilaAprovacao { get; set; } = [];
}

// Colaborador com saldo ainda não usado e o período concessivo perto do fim - risco de perda de
// direito (CLT art. 137, "regra 04" da política: não ultrapassar a data limite).
public class ConcessivoVencendoDto
{
    public int UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = string.Empty;
    public int PeriodoFeriasId { get; set; }
    public DateOnly FimConcessivo { get; set; }
    public int SaldoDisponivel { get; set; }
    public int DiasParaVencer { get; set; }
}

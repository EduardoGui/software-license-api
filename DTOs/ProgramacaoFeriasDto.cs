namespace SoftwareLicense.Api.DTOs;

public class ProgramacaoFeriasDto
{
    public int Id { get; set; }
    public int PeriodoFeriasId { get; set; }
    public int UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = string.Empty;
    public int Sequencia { get; set; }
    public DateOnly DataInicio { get; set; }
    public DateOnly DataFim { get; set; }
    public int QuantidadeDias { get; set; }
    public string Status { get; set; } = string.Empty;
    // Status calculado (Aprovada -> EmGozo/Concluida conforme as datas), nunca gravado.
    public string StatusEfetivo { get; set; } = string.Empty;
    public int? SolicitanteId { get; set; }
    public string? SolicitanteNome { get; set; }
    public DateOnly? DataSolicitacao { get; set; }
    public int? AprovadorId { get; set; }
    public string? AprovadorNome { get; set; }
    public DateTime? DataDecisao { get; set; }
    public string? ObservacaoAprovador { get; set; }
    public string? Observacao { get; set; }
    public bool AdiantamentoDecimoTerceiro { get; set; }
    public bool AbonoPecuniario { get; set; }
    public int DiasAbono { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}

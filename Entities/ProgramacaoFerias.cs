namespace SoftwareLicense.Api.Entities;

public class ProgramacaoFerias
{
    public int Id { get; set; }
    public int PeriodoFeriasId { get; set; }
    public PeriodoFerias PeriodoFerias { get; set; } = null!;
    // Ordem do fracionamento dentro do período (1º/2º/3º) - só numeração de exibição, nunca reaproveitada.
    public int Sequencia { get; set; }
    public DateOnly DataInicio { get; set; }
    public DateOnly DataFim { get; set; }
    public int QuantidadeDias { get; set; }
    // Rascunho | Solicitada | Aprovada | Reprovada | Cancelada (constantes em ProgramacaoFeriasStatus).
    // EmGozo/Concluida NUNCA são gravados aqui - são calculados na leitura a partir das datas quando
    // o status gravado é Aprovada (ver ProgramacaoFeriasService.CalcularStatusEfetivo).
    public string Status { get; set; } = string.Empty;
    public int? SolicitanteId { get; set; }
    public Usuario? Solicitante { get; set; }
    public DateOnly? DataSolicitacao { get; set; }
    public int? AprovadorId { get; set; }
    public Usuario? Aprovador { get; set; }
    public DateTime? DataDecisao { get; set; }
    public string? ObservacaoAprovador { get; set; }
    public string? Observacao { get; set; }
    // Só pode ser marcado na criação (não existe fluxo de edição de uma programação já criada).
    public bool AdiantamentoDecimoTerceiro { get; set; }
    public bool AbonoPecuniario { get; set; }
    public int DiasAbono { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}

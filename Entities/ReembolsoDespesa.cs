namespace SoftwareLicense.Api.Entities;

public class ReembolsoDespesa
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public int? SetorId { get; set; }
    public Setor? Setor { get; set; }
    public int? LocalId { get; set; }
    public Local? Local { get; set; }
    public DateOnly DataSolicitacao { get; set; }
    public string Finalidade { get; set; } = string.Empty;
    public string? FormaPagamento { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? AprovadorId { get; set; }
    public Usuario? Aprovador { get; set; }
    public string? ObservacaoAprovador { get; set; }
    public DateTime? DataDecisao { get; set; }
    public string? Observacao { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
    public List<ReembolsoDespesaItem> Itens { get; set; } = [];
}

namespace SoftwareLicense.Api.Entities;

public class Obrigacao
{
    public int Id { get; set; }
    public string TipoMovimento { get; set; } = string.Empty;
    public int? MedicaoBmId { get; set; }
    public MedicaoBm? MedicaoBm { get; set; }
    public int? OrdemCompraId { get; set; }
    public OrdemCompra? OrdemCompra { get; set; }
    public int? DespesaAvulsaId { get; set; }
    public DespesaAvulsa? DespesaAvulsa { get; set; }
    public int FornecedorId { get; set; }
    public Fornecedor Fornecedor { get; set; } = null!;
    public DateOnly Competencia { get; set; }
    public decimal ValorPrevisto { get; set; }
    public DateOnly? DataNf { get; set; }
    public string? NumeroNf { get; set; }
    public decimal? ValorNota { get; set; }
    public DateOnly? Vencimento { get; set; }
    public DateOnly? DataRequisicao { get; set; }
    public DateOnly? DataAssistPgto { get; set; }
    public DateOnly? DataEnvioFinanceiro { get; set; }
    public DateOnly? DataPrevistaPagamento { get; set; }
    public bool Pago { get; set; }
    public bool Cancelada { get; set; }
    public string? Observacoes { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}

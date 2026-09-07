namespace SoftwareLicense.Api.Entities;

public class DespesaAvulsa
{
    public int Id { get; set; }
    public int FornecedorId { get; set; }
    public Fornecedor Fornecedor { get; set; } = null!;
    public string Categoria { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string? NumeroNf { get; set; }
    public DateOnly? DataEmissao { get; set; }
    public DateOnly? Vencimento { get; set; }
    public decimal Valor { get; set; }
    public bool Recorrente { get; set; }
    public string? Observacoes { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}

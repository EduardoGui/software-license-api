namespace SoftwareLicense.Api.Entities;

public class OrdemCompra
{
    public int Id { get; set; }
    public int Numero { get; set; }
    public DateOnly Data { get; set; }
    public string Solicitante { get; set; } = string.Empty;
    public int LocalId { get; set; }
    public Local Local { get; set; } = null!;
    public int FornecedorId { get; set; }
    public Fornecedor Fornecedor { get; set; } = null!;
    public string CondicaoPagamento { get; set; } = string.Empty;
    public string? TipoFrete { get; set; }
    public decimal ValorFrete { get; set; }
    public string? LocalEntrega { get; set; }
    public string? PrazoEntrega { get; set; }
    public string? ObservacoesSolicitante { get; set; }
    public string? ObservacoesFornecedor { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }

    public List<OrdemCompraItem> Itens { get; set; } = [];
}

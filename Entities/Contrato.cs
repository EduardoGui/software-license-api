namespace SoftwareLicense.Api.Entities;

public class Contrato
{
    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public int FornecedorId { get; set; }
    public Fornecedor Fornecedor { get; set; } = null!;
    public string Objeto { get; set; } = string.Empty;
    public string? Natureza { get; set; }
    public DateOnly DataAssinatura { get; set; }
    public DateOnly DataInicioVigencia { get; set; }
    public DateOnly DataFimVigenciaOriginal { get; set; }
    public decimal ValorOriginal { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Observacoes { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }

    public List<ContratoItem> Itens { get; set; } = [];
}

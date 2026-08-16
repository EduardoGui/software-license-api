namespace SoftwareLicense.Api.Entities;

public class NotaFiscalEntrada
{
    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public DateOnly DataEntrada { get; set; }
    public string? FornecedorNome { get; set; }
    public string? Observacao { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }

    public ICollection<NotaFiscalItem> Itens { get; set; } = new List<NotaFiscalItem>();
}

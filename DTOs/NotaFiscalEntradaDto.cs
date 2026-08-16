namespace SoftwareLicense.Api.DTOs;

public class NotaFiscalEntradaDto
{
    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public DateOnly DataEntrada { get; set; }
    public string? FornecedorNome { get; set; }
    public string? Observacao { get; set; }
    public int QuantidadeItens { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}

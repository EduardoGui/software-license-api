namespace SoftwareLicense.Api.DTOs;

public class OrdemCompraDto
{
    public int Id { get; set; }
    public int Numero { get; set; }
    public DateOnly Data { get; set; }
    public string Solicitante { get; set; } = string.Empty;
    public int LocalId { get; set; }
    public string LocalNome { get; set; } = string.Empty;
    public int FornecedorId { get; set; }
    public string FornecedorNome { get; set; } = string.Empty;
    public string CondicaoPagamento { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal ValorTotal { get; set; }
    public int QuantidadeItens { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}

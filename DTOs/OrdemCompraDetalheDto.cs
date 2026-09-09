namespace SoftwareLicense.Api.DTOs;

public class OrdemCompraDetalheDto
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
    public string? TipoFrete { get; set; }
    public decimal ValorFrete { get; set; }
    public string? LocalEntrega { get; set; }
    public string? PrazoEntrega { get; set; }
    public string? ObservacoesSolicitante { get; set; }
    public string? ObservacoesFornecedor { get; set; }
    public string? ContatoAprovacaoNome { get; set; }
    public string? ContatoAprovacaoEmail { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal ValorTotal { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
    public List<OrdemCompraItemDto> Itens { get; set; } = [];
}

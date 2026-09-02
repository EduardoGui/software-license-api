namespace SoftwareLicense.Api.DTOs;

public class ContratoDto
{
    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public int FornecedorId { get; set; }
    public string FornecedorNome { get; set; } = string.Empty;
    public string Objeto { get; set; } = string.Empty;
    public string? Natureza { get; set; }
    public DateOnly DataAssinatura { get; set; }
    public DateOnly DataInicioVigencia { get; set; }
    public DateOnly DataFimVigenciaOriginal { get; set; }
    public DateOnly DataFimVigenciaAtual { get; set; }
    public decimal ValorOriginal { get; set; }
    public decimal ValorAtual { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Observacoes { get; set; }
    public int QuantidadeItens { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}

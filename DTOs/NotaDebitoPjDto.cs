namespace SoftwareLicense.Api.DTOs;

public class NotaDebitoPjDto
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = string.Empty;
    public string? EmpresaPjNome { get; set; }
    public string? EmpresaPjCnpj { get; set; }
    public int Ano { get; set; }
    public int Mes { get; set; }
    public decimal ValorBruto { get; set; }
    public decimal Desconto { get; set; }
    public decimal RetencaoTributaria { get; set; }
    public decimal ValorLiquido { get; set; }
    public string OperadoraSaude { get; set; } = string.Empty;
    public string? NumeroDocumento { get; set; }
    public string? Descricao { get; set; }
    public DateOnly? DataVencimento { get; set; }
    public string? FormaPagamento { get; set; }
    public string? CentroCusto { get; set; }
    public string? Area { get; set; }
    public string? ContaContabil { get; set; }
    public string? ProjetoContrato { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? DataEnvio { get; set; }
    public DateOnly? DataPagamento { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}

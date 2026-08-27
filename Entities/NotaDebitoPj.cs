namespace SoftwareLicense.Api.Entities;

public class NotaDebitoPj
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public int Ano { get; set; }
    public int Mes { get; set; }

    // Soma congelada das coparticipações (titular + dependentes) no momento da criação -
    // não recalcula depois, mesmo se o lançamento de PlanoSaudeCusto do mês mudar.
    public decimal ValorBruto { get; set; }
    public decimal Desconto { get; set; }
    public decimal RetencaoTributaria { get; set; }

    public string OperadoraSaude { get; set; } = string.Empty;
    public string? NumeroDocumento { get; set; }
    public string? Descricao { get; set; }

    public DateOnly? DataVencimento { get; set; }
    public string? FormaPagamento { get; set; }

    public string? CentroCusto { get; set; }
    public string? Area { get; set; }
    public string? ContaContabil { get; set; }
    public string? ProjetoContrato { get; set; }

    // Rascunho | Enviada | Paga (constantes em NotaDebitoPjStatus)
    public string Status { get; set; } = string.Empty;
    public DateTime? DataEnvio { get; set; }
    public DateOnly? DataPagamento { get; set; }

    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}

namespace SoftwareLicense.Api.Entities;

// Snapshot congelado, no momento da criação da ND, de cada beneficiário (titular + dependentes) que
// compôs o ValorBruto daquele mês - não recalcula depois, mesmo se o PlanoSaudeCusto do mês mudar.
public class NotaDebitoPjItem
{
    public int Id { get; set; }
    public int NotaDebitoPjId { get; set; }
    public NotaDebitoPj NotaDebitoPj { get; set; } = null!;

    // Null = titular; preenchido = dependente específico.
    public int? DependenteId { get; set; }
    public Dependente? Dependente { get; set; }

    public string NomeBeneficiario { get; set; } = string.Empty;
    public decimal ValorMensalidade { get; set; }
    public decimal ValorCoparticipacao { get; set; }
    public DateTime DataCriacao { get; set; }
}

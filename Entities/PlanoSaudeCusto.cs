namespace SoftwareLicense.Api.Entities;

public class PlanoSaudeCusto
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    // Null = lançamento do titular; preenchido = lançamento de um dependente específico dele.
    public int? DependenteId { get; set; }
    public Dependente? Dependente { get; set; }
    public int Ano { get; set; }
    public int Mes { get; set; }
    public decimal ValorMensal { get; set; }
    public decimal ValorCoparticipacao { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}

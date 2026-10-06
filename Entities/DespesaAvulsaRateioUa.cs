namespace SoftwareLicense.Api.Entities;

public class DespesaAvulsaRateioUa
{
    public int Id { get; set; }
    public int DespesaAvulsaId { get; set; }
    public DespesaAvulsa DespesaAvulsa { get; set; } = null!;
    public int UnidadeOrcamentariaId { get; set; }
    public UnidadeOrcamentaria UnidadeOrcamentaria { get; set; } = null!;
    public decimal Valor { get; set; }
    public DateTime DataCriacao { get; set; }
}

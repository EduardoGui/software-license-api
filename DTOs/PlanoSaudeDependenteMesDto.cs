namespace SoftwareLicense.Api.DTOs;

public class PlanoSaudeDependenteMesDto
{
    public int DependenteId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int? LancamentoId { get; set; }
    public decimal? ValorMensal { get; set; }
    public decimal? ValorCoparticipacao { get; set; }
}

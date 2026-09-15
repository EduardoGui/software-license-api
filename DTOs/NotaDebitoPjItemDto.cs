namespace SoftwareLicense.Api.DTOs;

public class NotaDebitoPjItemDto
{
    public int? DependenteId { get; set; }
    public string NomeBeneficiario { get; set; } = string.Empty;
    public decimal ValorMensalidade { get; set; }
    public decimal ValorCoparticipacao { get; set; }
}

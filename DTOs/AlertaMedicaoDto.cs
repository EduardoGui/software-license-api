namespace SoftwareLicense.Api.DTOs;

public class AlertaMedicaoDto
{
    public int ContratoId { get; set; }
    public string ContratoNumero { get; set; } = string.Empty;
    public string FornecedorNome { get; set; } = string.Empty;
    public DateOnly PeriodoFim { get; set; }
    public int DiasParaVencer { get; set; }
}

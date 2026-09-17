namespace SoftwareLicense.Api.DTOs;

public class ObrigacaoFiltroDto
{
    public DateOnly? CompetenciaDe { get; set; }
    public DateOnly? CompetenciaAte { get; set; }
    public string? TipoMovimento { get; set; }
    public int? FornecedorId { get; set; }
    public int? ContratoId { get; set; }
    public int? OrdemCompraId { get; set; }
    public string? Etapa { get; set; }
    public bool? Pago { get; set; }
    public bool? Cancelada { get; set; }
}

namespace SoftwareLicense.Api.DTOs;

public class EquipamentoFiltroDto
{
    public int? TipoEquipamentoId { get; set; }
    public string? Origem { get; set; }
    public string? Status { get; set; }
    public int? UsuarioId { get; set; }
    public int? NotaFiscalEntradaId { get; set; }
}

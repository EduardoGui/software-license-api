namespace SoftwareLicense.Api.DTOs;

public class EquipamentoAlocacaoFiltroDto
{
    public int? UsuarioId { get; set; }
    public int? EquipamentoId { get; set; }
    public DateOnly? DataInicial { get; set; }
    public DateOnly? DataFinal { get; set; }
    public string? Status { get; set; }
    public int Pagina { get; set; } = 1;
    public int TamanhoPagina { get; set; } = 10;
}

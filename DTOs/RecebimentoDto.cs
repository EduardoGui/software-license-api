namespace SoftwareLicense.Api.DTOs;

public class RecebimentoDto
{
    public string CampanhaNome { get; set; } = string.Empty;
    public string UsuarioNome { get; set; } = string.Empty;
    public DateOnly? DataEntregaFisica { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<EntregaItemDto> Itens { get; set; } = [];

    public DateTime? DataConfirmacao { get; set; }
    public string? TipoDivergencia { get; set; }
    public string? ObservacaoDivergencia { get; set; }
}

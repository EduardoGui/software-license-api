namespace SoftwareLicense.Api.DTOs;

public class EntregaDto
{
    public int Id { get; set; }
    public int CampanhaEntregaId { get; set; }
    public int UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = string.Empty;
    public string EmailDestino { get; set; } = string.Empty;

    public DateOnly? DataEntregaFisica { get; set; }
    public int? ResponsavelEntregaId { get; set; }
    public string? ResponsavelEntregaNome { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime? DataEnvioEmail { get; set; }
    public DateTime? DataAcessoLink { get; set; }
    public string? IpAcessoLink { get; set; }
    public string? UserAgentAcessoLink { get; set; }
    public DateTime? DataConfirmacao { get; set; }
    public string? IpConfirmacao { get; set; }
    public string? UserAgentConfirmacao { get; set; }

    public string? TipoDivergencia { get; set; }
    public string? ObservacaoDivergencia { get; set; }

    public string? AvisoEmail { get; set; }

    public List<EntregaItemDto> Itens { get; set; } = [];

    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}

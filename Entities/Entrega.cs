namespace SoftwareLicense.Api.Entities;

public class Entrega
{
    public int Id { get; set; }
    public int CampanhaEntregaId { get; set; }
    public CampanhaEntrega CampanhaEntrega { get; set; } = null!;
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public string EmailDestino { get; set; } = string.Empty;

    public DateOnly? DataEntregaFisica { get; set; }
    public int? ResponsavelEntregaId { get; set; }
    public Usuario? ResponsavelEntrega { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? TokenHash { get; set; }
    public DateTime? DataEnvioEmail { get; set; }

    public DateTime? DataAcessoLink { get; set; }
    public string? IpAcessoLink { get; set; }
    public string? UserAgentAcessoLink { get; set; }

    public DateTime? DataConfirmacao { get; set; }
    public string? IpConfirmacao { get; set; }
    public string? UserAgentConfirmacao { get; set; }

    public string? TipoDivergencia { get; set; }
    public string? ObservacaoDivergencia { get; set; }

    // Quantos kits essa entrega representa (padrão 1) - cobre o caso de diretores/gerentes que pegam
    // vários de uma vez, em nome deles, pra repassar a clientes. Multiplica a quantidade de cada item
    // copiado da campanha no momento da criação (ver CopiarItensDaCampanha).
    public int QuantidadeKits { get; set; } = 1;
    public string? Observacao { get; set; }

    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }

    public List<EntregaItem> Itens { get; set; } = [];
}

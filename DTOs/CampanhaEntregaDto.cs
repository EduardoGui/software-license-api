namespace SoftwareLicense.Api.DTOs;

public class CampanhaEntregaDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }

    public List<CampanhaEntregaItemDto> Itens { get; set; } = [];
    // Histórico das entradas de itens comprados (mais recentes primeiro).
    public List<CampanhaEntregaEntradaDto> Entradas { get; set; } = [];
}

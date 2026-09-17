namespace SoftwareLicense.Api.DTOs;

public class CampanhaEntregaDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }

    public List<CampanhaEntregaItemDto> Itens { get; set; } = [];
}

namespace SoftwareLicense.Api.Entities;

public class CampanhaEntrega
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }

    public List<Entrega> Entregas { get; set; } = [];
    public List<CampanhaEntregaItem> Itens { get; set; } = [];
}

namespace SoftwareLicense.Api.Entities;

public class EntregaItem
{
    public int Id { get; set; }
    public int EntregaId { get; set; }
    public Entrega Entrega { get; set; } = null!;
    public string Descricao { get; set; } = string.Empty;
    public string? Tamanho { get; set; }
    public int Quantidade { get; set; }
    public DateOnly? Validade { get; set; }
    public DateTime DataCriacao { get; set; }
}

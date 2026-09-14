namespace SoftwareLicense.Api.Entities;

// Lista "padrão" de itens da campanha - copiada pra cada Entrega no momento em que o colaborador é
// adicionado (snapshot), pra não exigir redigitar a lista a cada colaborador/lote. Editar aqui só
// afeta quem for adicionado dali pra frente; quem já foi adicionado mantém seu próprio EntregaItem.
public class CampanhaEntregaItem
{
    public int Id { get; set; }
    public int CampanhaEntregaId { get; set; }
    public CampanhaEntrega CampanhaEntrega { get; set; } = null!;
    public string Descricao { get; set; } = string.Empty;
    public string? Tamanho { get; set; }
    public int Quantidade { get; set; }
    public DateOnly? Validade { get; set; }
    public DateTime DataCriacao { get; set; }
}

namespace SoftwareLicense.Api.Entities;

public class EntregaItem
{
    public int Id { get; set; }
    public int EntregaId { get; set; }
    public Entrega Entrega { get; set; } = null!;

    // Vínculo com o item do catálogo da campanha - é por ele que o saldo de estoque é calculado.
    // Nulo em itens legados que não casaram com nenhum item do catálogo.
    public int? CampanhaEntregaItemId { get; set; }
    public CampanhaEntregaItem? CampanhaEntregaItem { get; set; }

    public string Descricao { get; set; } = string.Empty;
    public string? Tamanho { get; set; }
    public int Quantidade { get; set; }
    public DateOnly? Validade { get; set; }

    // Preenchido nos itens que saíram numa troca (ver EntregaDevolucao); nulo nos itens da entrega original.
    public int? EntregaDevolucaoId { get; set; }
    public EntregaDevolucao? EntregaDevolucao { get; set; }

    // Devoluções registradas contra este item (a entrega continua Confirmada; Quantidade é o que foi entregue originalmente).
    public List<EntregaDevolucaoItem> Devolucoes { get; set; } = [];

    // Calculados (não mapeados): o que o colaborador ainda tem, e o que voltou ao estoque da campanha.
    public int QuantidadeDevolvida => Devolucoes.Sum(d => d.Quantidade);
    public int QuantidadeAtual => Quantidade - QuantidadeDevolvida;
    public int QuantidadeDevolvidaAoEstoque => Devolucoes.Where(d => d.VoltaAoEstoque).Sum(d => d.Quantidade);

    public DateTime DataCriacao { get; set; }
}

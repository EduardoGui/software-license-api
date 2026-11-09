namespace SoftwareLicense.Api.Entities;

// Troca ou devolução de itens de uma entrega já Confirmada (ex.: pegou camisa G, devolveu a G e pegou a P).
// A entrega continua Confirmada; este registro guarda o histórico: o que voltou (Itens) e o que saiu no lugar
// (ItensRetirados, que também são EntregaItem da mesma entrega).
public class EntregaDevolucao
{
    public int Id { get; set; }
    public int EntregaId { get; set; }
    public Entrega Entrega { get; set; } = null!;

    public DateOnly Data { get; set; }
    // Troca | Devolucao | Defeito | Outro (ver EntregaDevolucaoMotivo).
    public string Motivo { get; set; } = string.Empty;
    public string? Observacao { get; set; }

    // Quem registrou (colaborador vinculado à conta, quando houver); o nome fica gravado para o histórico.
    public int? UsuarioId { get; set; }
    public string? RegistradoPorNome { get; set; }
    public DateTime DataCriacao { get; set; }

    // Itens devolvidos (quantidade e se voltaram ao estoque).
    public List<EntregaDevolucaoItem> Itens { get; set; } = [];
    // Itens novos retirados nesta troca (EntregaItem.EntregaDevolucaoId aponta para cá).
    public List<EntregaItem> ItensRetirados { get; set; } = [];
}

public class EntregaDevolucaoItem
{
    public int Id { get; set; }
    public int EntregaDevolucaoId { get; set; }
    public EntregaDevolucao EntregaDevolucao { get; set; } = null!;
    public int EntregaItemId { get; set; }
    public EntregaItem EntregaItem { get; set; } = null!;
    public int Quantidade { get; set; }
    // true = a peça volta ao estoque da campanha (aumenta o saldo); false = descartada (ex.: usada ou com defeito).
    public bool VoltaAoEstoque { get; set; } = true;
}

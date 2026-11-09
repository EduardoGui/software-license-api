namespace SoftwareLicense.Api.Entities;

// "Nova entrada de itens comprados": reposição de estoque de um item da campanha (ex.: compramos mais camisas G).
// Cada entrada é um registro próprio (quando, quanto, nota/observação, quem). O estoque total do item é sempre
// o estoque anterior (CampanhaEntregaItem.QuantidadeDisponivel, informado na criação da campanha) + soma das entradas;
// o saldo é calculado a partir dele, nunca armazenado.
public class CampanhaEntregaEntrada
{
    public int Id { get; set; }
    public int CampanhaEntregaItemId { get; set; }
    public CampanhaEntregaItem CampanhaEntregaItem { get; set; } = null!;

    // Data da compra/recebimento da remessa.
    public DateOnly Data { get; set; }
    public int Quantidade { get; set; }

    // Livre: nº da nota fiscal, pedido, fornecedor...
    public string? Observacao { get; set; }

    // Quem registrou (colaborador vinculado à conta, quando houver); o nome fica gravado junto para o histórico não depender do cadastro.
    public int? UsuarioId { get; set; }
    public string? RegistradoPorNome { get; set; }
    public DateTime DataCriacao { get; set; }
}

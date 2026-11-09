namespace SoftwareLicense.Api.DTOs;

public class CampanhaEntregaItemDto
{
    public int Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string? Tamanho { get; set; }
    public int Quantidade { get; set; }
    public DateOnly? Validade { get; set; }

    // Estoque ANTERIOR (informado na criação da campanha).
    public int? QuantidadeDisponivel { get; set; }
    // Soma das "novas entradas de itens comprados" deste item.
    public int QuantidadeEntradas { get; set; }
    // Estoque total = anterior + entradas (null quando o item não controla estoque).
    public int? EstoqueTotal { get; set; }

    // Quantidade já atribuída em entregas não canceladas (reservada, inclusive pendente).
    public int QuantidadeEntregue { get; set; }

    // Null quando QuantidadeDisponivel não foi definido (campanha não controla saldo desse item).
    public int? SaldoDisponivel { get; set; }
}

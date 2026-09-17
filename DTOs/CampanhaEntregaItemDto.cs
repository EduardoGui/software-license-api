namespace SoftwareLicense.Api.DTOs;

public class CampanhaEntregaItemDto
{
    public int Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string? Tamanho { get; set; }
    public int Quantidade { get; set; }
    public DateOnly? Validade { get; set; }

    public int? QuantidadeDisponivel { get; set; }
    public int QuantidadeEntregue { get; set; }

    // Null quando QuantidadeDisponivel não foi definido (campanha não controla saldo desse item).
    public int? SaldoDisponivel { get; set; }
}

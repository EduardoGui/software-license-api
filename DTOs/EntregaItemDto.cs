namespace SoftwareLicense.Api.DTOs;

public class EntregaItemDto
{
    public int Id { get; set; }
    public int? CampanhaEntregaItemId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string? Tamanho { get; set; }
    public int Quantidade { get; set; }
    public DateOnly? Validade { get; set; }
    // Quanto deste item já foi devolvido/trocado (Quantidade é o entregue originalmente).
    public int QuantidadeDevolvida { get; set; }
    // Preenchido quando o item saiu numa troca/devolução (id do registro de troca).
    public int? EntregaDevolucaoId { get; set; }
}

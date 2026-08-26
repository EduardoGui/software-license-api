namespace SoftwareLicense.Api.DTOs;

public class ReembolsoDespesaItemDto
{
    public int Id { get; set; }
    public DateOnly Data { get; set; }
    public int TipoDespesaId { get; set; }
    public string TipoDespesaNome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public string? NumeroDocumento { get; set; }
    public decimal Valor { get; set; }
    public List<AnexoDto> Anexos { get; set; } = [];
}

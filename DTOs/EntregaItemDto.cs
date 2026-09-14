namespace SoftwareLicense.Api.DTOs;

public class EntregaItemDto
{
    public int Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string? Tamanho { get; set; }
    public int Quantidade { get; set; }
    public DateOnly? Validade { get; set; }
}

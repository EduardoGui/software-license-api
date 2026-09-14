namespace SoftwareLicense.Api.DTOs;

// Versão enxuta da Entrega pro colaborador ver seu próprio histórico (app mobile) - sem os campos
// administrativos (IP, User-Agent, responsável pela entrega física etc.), só o que interessa a
// quem recebeu: o quê, quando, e o desfecho (confirmado ou com divergência).
public class MinhaEntregaDto
{
    public int Id { get; set; }
    public string CampanhaNome { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateOnly? DataEntregaFisica { get; set; }
    public DateTime? DataConfirmacao { get; set; }
    public List<EntregaItemDto> Itens { get; set; } = [];
}

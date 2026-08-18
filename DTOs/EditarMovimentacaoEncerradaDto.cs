namespace SoftwareLicense.Api.DTOs;

public class EditarMovimentacaoEncerradaDto
{
    public DateOnly? DataFim { get; set; }

    public string? Observacao { get; set; }
}

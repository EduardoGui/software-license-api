namespace SoftwareLicense.Api.DTOs;

public class TarefaOcorrenciaDto
{
    public int Id { get; set; }
    public int? TarefaRecorrenteId { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public DateOnly DataPrevistaOriginal { get; set; }
    public DateOnly DataPrevistaAtual { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? DataConclusao { get; set; }
    public string? Observacao { get; set; }
    public int DiasParaVencer { get; set; }
}

namespace SoftwareLicense.Api.Entities;

public class TarefaOcorrencia
{
    public int Id { get; set; }
    public int TarefaRecorrenteId { get; set; }
    public TarefaRecorrente TarefaRecorrente { get; set; } = null!;

    // Sempre o dia 1 do mês a que a ocorrência se refere — junto com TarefaRecorrenteId, garante
    // (via índice único) que nunca nasce mais de uma ocorrência do mesmo mês pra mesma tarefa.
    public DateOnly MesReferencia { get; set; }

    public DateOnly DataPrevistaOriginal { get; set; }
    public DateOnly DataPrevistaAtual { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? DataConclusao { get; set; }
    public string? Observacao { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}

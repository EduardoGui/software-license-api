namespace SoftwareLicense.Api.Entities;

public class TarefaRecorrente
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public int DiaDoMes { get; set; }
    public string? Observacao { get; set; }
    public bool Ativa { get; set; } = true;
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }

    public List<TarefaOcorrencia> Ocorrencias { get; set; } = [];
}

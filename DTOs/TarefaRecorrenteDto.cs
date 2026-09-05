namespace SoftwareLicense.Api.DTOs;

public class TarefaRecorrenteDto
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public int DiaDoMes { get; set; }
    public string? Observacao { get; set; }
    public bool Ativa { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}

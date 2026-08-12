namespace SoftwareLicense.Api.Entities;

public class Licenca
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public int QuantidadeTotal { get; set; }
    public DateOnly DataInicio { get; set; }
    public DateOnly DataTerminoPrevisto { get; set; }
    public int DiasAntecedenciaAviso { get; set; }
    public string? Observacao { get; set; }
    public bool Ativa { get; set; } = true;
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}

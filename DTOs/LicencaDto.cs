namespace SoftwareLicense.Api.DTOs;

public class LicencaDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public int QuantidadeTotal { get; set; }
    public int QuantidadeEmUso { get; set; }
    public int QuantidadeDisponivel { get; set; }
    public DateOnly DataInicio { get; set; }
    public DateOnly DataTerminoPrevisto { get; set; }
    public int DiasAntecedenciaAviso { get; set; }
    public string? Observacao { get; set; }
    public bool Ativa { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}

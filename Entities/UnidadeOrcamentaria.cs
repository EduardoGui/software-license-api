namespace SoftwareLicense.Api.Entities;

public class UnidadeOrcamentaria
{
    public int Id { get; set; }
    public int SetorId { get; set; }
    public Setor Setor { get; set; } = null!;
    public string Codigo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string? Apropriacao { get; set; }
    public bool Ativa { get; set; } = true;
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}

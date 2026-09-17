namespace SoftwareLicense.Api.DTOs;

public class UnidadeOrcamentariaDto
{
    public int Id { get; set; }
    public int SetorId { get; set; }
    public string SetorNome { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string? Apropriacao { get; set; }
    public bool Ativa { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}

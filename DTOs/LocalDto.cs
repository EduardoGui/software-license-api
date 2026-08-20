namespace SoftwareLicense.Api.DTOs;

public class LocalDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Endereco { get; set; }
    public bool Ativo { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}

namespace SoftwareLicense.Api.Entities;

public class Local
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Endereco { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}

namespace SoftwareLicense.Api.Entities;

public class Dependente
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public string Nome { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}

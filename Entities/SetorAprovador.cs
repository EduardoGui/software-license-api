namespace SoftwareLicense.Api.Entities;

public class SetorAprovador
{
    public int Id { get; set; }
    public int SetorId { get; set; }
    public Setor Setor { get; set; } = null!;
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public DateTime DataCriacao { get; set; }
}

namespace SoftwareLicense.Api.Entities;

public class UsuarioLicenca
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public int LicencaId { get; set; }
    public Licenca Licenca { get; set; } = null!;
    public DateOnly DataInicio { get; set; }
    public DateOnly? DataFim { get; set; }
    public string? Observacao { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}

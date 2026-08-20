namespace SoftwareLicense.Api.DTOs;

public class SetorAprovadorDto
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = string.Empty;
}

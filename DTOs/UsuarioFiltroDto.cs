namespace SoftwareLicense.Api.DTOs;

public class UsuarioFiltroDto
{
    public string? Nome { get; set; }
    public string? Email { get; set; }
    public string? Status { get; set; }
    public string? Tipo { get; set; }
    public int? SetorId { get; set; }
}

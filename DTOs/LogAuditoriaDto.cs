namespace SoftwareLicense.Api.DTOs;

public class LogAuditoriaDto
{
    public int Id { get; set; }
    public DateTime DataHora { get; set; }
    public int? UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = string.Empty;
    public string Entidade { get; set; } = string.Empty;
    public int EntidadeId { get; set; }
    public string Acao { get; set; } = string.Empty;
    public string? Detalhe { get; set; }
}

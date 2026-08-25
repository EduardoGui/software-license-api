namespace SoftwareLicense.Api.DTOs;

public class LogAuditoriaFiltroDto
{
    public DateOnly? DataInicial { get; set; }
    public DateOnly? DataFinal { get; set; }
    public string? Entidade { get; set; }
    public int? EntidadeId { get; set; }
    public int? UsuarioId { get; set; }
}

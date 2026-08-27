namespace SoftwareLicense.Api.DTOs;

public class RelatorioMensalPlanoSaudeItemDto
{
    public int UsuarioId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? SetorNome { get; set; }
    public string? EmpresaPjNome { get; set; }
    public decimal ValorTotal { get; set; }
}

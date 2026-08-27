namespace SoftwareLicense.Api.DTOs;

public class RelatorioMensalPlanoSaudeDto
{
    public int Ano { get; set; }
    public int Mes { get; set; }
    public List<RelatorioMensalPlanoSaudeItemDto> Itens { get; set; } = [];
    public decimal ValorTotal { get; set; }
}

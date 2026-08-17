namespace SoftwareLicense.Api.DTOs;

public class RelatorioMensalLocacaoDto
{
    public int Ano { get; set; }
    public int Mes { get; set; }
    public List<RelatorioMensalLocacaoItemDto> Itens { get; set; } = [];
    public decimal TotalGeral { get; set; }
}

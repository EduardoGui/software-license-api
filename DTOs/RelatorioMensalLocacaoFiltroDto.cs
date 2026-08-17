namespace SoftwareLicense.Api.DTOs;

public class RelatorioMensalLocacaoFiltroDto
{
    public int Ano { get; set; }
    public int Mes { get; set; }
    public string? FornecedorNome { get; set; }
    public int? TipoEquipamentoId { get; set; }
}

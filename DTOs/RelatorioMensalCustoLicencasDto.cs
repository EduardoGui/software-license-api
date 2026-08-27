namespace SoftwareLicense.Api.DTOs;

public class RelatorioMensalCustoLicencasDto
{
    public int Ano { get; set; }
    public int Mes { get; set; }
    public List<RelatorioMensalCustoLicencasGrupoDto> Grupos { get; set; } = [];

    // "Medição da empresa" - valor total do mês somando todos os grupos/licenças.
    public decimal ValorTotal { get; set; }
}

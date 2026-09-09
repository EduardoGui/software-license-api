namespace SoftwareLicense.Api.DTOs;

public class RelatorioMensalCustoLicencasDto
{
    public int Ano { get; set; }
    public int Mes { get; set; }

    // Licenças com LicencaValor.Periodicidade = "Mensal" — cobrança recorrente todo mês.
    public List<RelatorioMensalCustoLicencasGrupoDto> GruposMensal { get; set; } = [];
    public decimal SubtotalMensal { get; set; }

    // Licenças com LicencaValor.Periodicidade = "Anual" — cobrança uma vez por ano; o valor aqui
    // é o equivalente mensal (Valor/12) só para permitir comparação, não uma cobrança do mês.
    public List<RelatorioMensalCustoLicencasGrupoDto> GruposAnual { get; set; } = [];
    public decimal SubtotalAnual { get; set; }

    // "Medição da empresa" - soma de SubtotalMensal + SubtotalAnual.
    public decimal ValorTotal { get; set; }
}

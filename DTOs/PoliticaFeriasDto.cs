namespace SoftwareLicense.Api.DTOs;

public class PoliticaFeriasDto
{
    public int Id { get; set; }
    public string? TipoVinculo { get; set; }
    public int DiasDireitoPorAno { get; set; }
    public int MaxFracionamentos { get; set; }
    public int DiasMinimoUltimoFracionamento { get; set; }
    public int DiasMinimoDemaisFracionamentos { get; set; }
    public int DiasAntecedenciaRemarcacao { get; set; }
    public int DiasAntecedenciaMarcacaoCompulsoria { get; set; }
    public bool PermiteAbonoPecuniario { get; set; }
    public int MaxDiasAbono { get; set; }
    public int DiasMinimosAntesFeriadoOuFimDeSemana { get; set; }
    public bool Ativa { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}

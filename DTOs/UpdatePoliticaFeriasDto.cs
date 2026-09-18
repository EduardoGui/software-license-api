using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class UpdatePoliticaFeriasDto
{
    public string? TipoVinculo { get; set; }

    [Range(1, 60, ErrorMessage = "Dias de direito por ano deve estar entre 1 e 60.")]
    public int DiasDireitoPorAno { get; set; }

    [Range(1, 10, ErrorMessage = "Máximo de fracionamentos deve estar entre 1 e 10.")]
    public int MaxFracionamentos { get; set; }

    [Range(1, 30, ErrorMessage = "Dias mínimos do último fracionamento devem estar entre 1 e 30.")]
    public int DiasMinimoUltimoFracionamento { get; set; }

    [Range(1, 30, ErrorMessage = "Dias mínimos dos demais fracionamentos devem estar entre 1 e 30.")]
    public int DiasMinimoDemaisFracionamentos { get; set; }

    [Range(0, 365, ErrorMessage = "Dias de antecedência para remarcação devem estar entre 0 e 365.")]
    public int DiasAntecedenciaRemarcacao { get; set; }

    [Range(0, 365, ErrorMessage = "Dias de antecedência para marcação compulsória devem estar entre 0 e 365.")]
    public int DiasAntecedenciaMarcacaoCompulsoria { get; set; }

    public bool PermiteAbonoPecuniario { get; set; }

    [Range(0, 30, ErrorMessage = "Máximo de dias de abono deve estar entre 0 e 30.")]
    public int MaxDiasAbono { get; set; }

    [Range(0, 30, ErrorMessage = "Dias mínimos antes de feriado/fim de semana devem estar entre 0 e 30.")]
    public int DiasMinimosAntesFeriadoOuFimDeSemana { get; set; }

    public bool Ativa { get; set; }
}

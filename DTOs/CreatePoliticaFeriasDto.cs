using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreatePoliticaFeriasDto
{
    // Pj | Clt | Estagio (constantes em UsuarioTipo), ou null = política padrão geral.
    public string? TipoVinculo { get; set; }

    [Range(1, 60, ErrorMessage = "Dias de direito por ano deve estar entre 1 e 60.")]
    public int DiasDireitoPorAno { get; set; } = 30;

    [Range(1, 10, ErrorMessage = "Máximo de fracionamentos deve estar entre 1 e 10.")]
    public int MaxFracionamentos { get; set; } = 3;

    [Range(1, 30, ErrorMessage = "Dias mínimos do último fracionamento devem estar entre 1 e 30.")]
    public int DiasMinimoUltimoFracionamento { get; set; } = 14;

    [Range(1, 30, ErrorMessage = "Dias mínimos dos demais fracionamentos devem estar entre 1 e 30.")]
    public int DiasMinimoDemaisFracionamentos { get; set; } = 5;

    [Range(0, 365, ErrorMessage = "Dias de antecedência para remarcação devem estar entre 0 e 365.")]
    public int DiasAntecedenciaRemarcacao { get; set; } = 45;

    [Range(0, 365, ErrorMessage = "Dias de antecedência para marcação compulsória devem estar entre 0 e 365.")]
    public int DiasAntecedenciaMarcacaoCompulsoria { get; set; } = 30;

    public bool PermiteAbonoPecuniario { get; set; } = true;

    [Range(0, 30, ErrorMessage = "Máximo de dias de abono deve estar entre 0 e 30.")]
    public int MaxDiasAbono { get; set; } = 10;

    [Range(0, 30, ErrorMessage = "Dias mínimos antes de feriado/fim de semana devem estar entre 0 e 30.")]
    public int DiasMinimosAntesFeriadoOuFimDeSemana { get; set; } = 2;

    public bool Ativa { get; set; } = true;
}

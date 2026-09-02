using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateContratoFaturamentoConfigDto
{
    [Range(1, 31)]
    public int DiaInicialJanelaNf { get; set; } = 1;

    [Range(1, 31)]
    public int DiaFinalJanelaNf { get; set; } = 24;

    public bool ExigeBmAprovado { get; set; }
    public bool ExigeBmAssinado { get; set; }

    [Range(0, 365)]
    public int? PrazoPagamentoDias { get; set; }
}

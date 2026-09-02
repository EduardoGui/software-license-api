namespace SoftwareLicense.Api.DTOs;

public class ContratoFaturamentoConfigDto
{
    public int DiaInicialJanelaNf { get; set; }
    public int DiaFinalJanelaNf { get; set; }
    public bool ExigeBmAprovado { get; set; }
    public bool ExigeBmAssinado { get; set; }
    public int? PrazoPagamentoDias { get; set; }
}

namespace SoftwareLicense.Api.Entities;

public class ContratoFaturamentoConfig
{
    public int Id { get; set; }
    public int ContratoId { get; set; }
    public Contrato Contrato { get; set; } = null!;
    public int DiaInicialJanelaNf { get; set; } = 1;
    public int DiaFinalJanelaNf { get; set; } = 24;
    public bool ExigeBmAprovado { get; set; }
    public bool ExigeBmAssinado { get; set; }
    public int? PrazoPagamentoDias { get; set; }
}

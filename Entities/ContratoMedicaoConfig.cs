namespace SoftwareLicense.Api.Entities;

public class ContratoMedicaoConfig
{
    public int Id { get; set; }
    public int ContratoId { get; set; }
    public Contrato Contrato { get; set; } = null!;
    public string TipoMedicao { get; set; } = string.Empty;
    public int? DiaInicioPeriodo { get; set; }
    public int? DiaFimPeriodo { get; set; }
    public bool ExigeBm { get; set; }
    public bool ExigeAprovacao { get; set; }
    public bool ExigeAssinatura { get; set; }
    public bool PermiteProRata { get; set; }
    public string? MetodoProRata { get; set; }
    public int? DiasAntecedenciaAlerta { get; set; }
}

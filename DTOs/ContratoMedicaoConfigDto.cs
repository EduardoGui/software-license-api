namespace SoftwareLicense.Api.DTOs;

public class ContratoMedicaoConfigDto
{
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

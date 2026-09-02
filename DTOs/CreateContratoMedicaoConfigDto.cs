using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

public class CreateContratoMedicaoConfigDto
{
    [Required(ErrorMessage = "Tipo de medição é obrigatório.")]
    public string TipoMedicao { get; set; } = string.Empty;

    [Range(1, 31)]
    public int? DiaInicioPeriodo { get; set; }

    [Range(1, 31)]
    public int? DiaFimPeriodo { get; set; }

    public bool ExigeBm { get; set; }
    public bool ExigeAprovacao { get; set; }
    public bool ExigeAssinatura { get; set; }
    public bool PermiteProRata { get; set; }
    public string? MetodoProRata { get; set; }

    [Range(0, 365)]
    public int? DiasAntecedenciaAlerta { get; set; }
}

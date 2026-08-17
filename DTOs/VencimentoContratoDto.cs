namespace SoftwareLicense.Api.DTOs;

public class VencimentoContratoDto
{
    public int EquipamentoId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public DateOnly DataFimContrato { get; set; }
    public int DiasParaVencer { get; set; }
}

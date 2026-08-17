namespace SoftwareLicense.Api.DTOs;

public class InventarioGrupoDto
{
    public int TipoEquipamentoId { get; set; }
    public string TipoEquipamentoNome { get; set; } = string.Empty;
    public List<EquipamentoDto> Itens { get; set; } = [];
    public int TotalDisponivel { get; set; }
    public int TotalEmUso { get; set; }
    public int TotalManutencao { get; set; }
    public int TotalBaixado { get; set; }
}

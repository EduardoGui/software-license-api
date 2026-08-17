namespace SoftwareLicense.Api.DTOs;

public class InventarioDto
{
    public List<InventarioGrupoDto> Grupos { get; set; } = [];
    public int TotalGeral { get; set; }
}

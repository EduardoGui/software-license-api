namespace SoftwareLicense.Api.DTOs;

public class UnidadeOrcamentariaFiltroDto
{
    public int? SetorId { get; set; }
    public string? Codigo { get; set; }
    public string? Descricao { get; set; }
    public string? Apropriacao { get; set; }
    public bool? Ativa { get; set; }
}

namespace SoftwareLicense.Api.DTOs;

public class UpdateMedicaoBmDto
{
    public string? NumeroReferencia { get; set; }
    public DateOnly? DataEnvio { get; set; }
    public string? Observacao { get; set; }
    public List<UpdateMedicaoBmItemDto> Itens { get; set; } = [];
    public List<UpdateMedicaoBmAcertoDto> Acertos { get; set; } = [];
    public List<UpdateMedicaoBmImpostoDto> Impostos { get; set; } = [];
}

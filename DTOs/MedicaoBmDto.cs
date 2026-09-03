namespace SoftwareLicense.Api.DTOs;

public class MedicaoBmDto
{
    public int Id { get; set; }
    public int ContratoId { get; set; }
    public int Numero { get; set; }
    public string? NumeroReferencia { get; set; }
    public DateOnly PeriodoInicio { get; set; }
    public DateOnly PeriodoFim { get; set; }
    public DateOnly? DataEnvio { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? AprovadorId { get; set; }
    public string? AprovadorNome { get; set; }
    public string? ObservacaoAprovador { get; set; }
    public DateTime? DataDecisao { get; set; }
    public decimal ValorTotalMedido { get; set; }
    public decimal ValorTotalAcertos { get; set; }
    public decimal ValorTotalImpostos { get; set; }
    public decimal ValorLiquido { get; set; }
    public string? Observacao { get; set; }
    public DateTime DataCriacao { get; set; }
    public List<MedicaoBmItemDto> Itens { get; set; } = [];
    public List<MedicaoBmAcertoDto> Acertos { get; set; } = [];
    public List<MedicaoBmImpostoDto> Impostos { get; set; } = [];
}

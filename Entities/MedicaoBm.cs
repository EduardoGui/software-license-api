namespace SoftwareLicense.Api.Entities;

public class MedicaoBm
{
    public int Id { get; set; }
    public int ContratoId { get; set; }
    public Contrato Contrato { get; set; } = null!;
    public int Numero { get; set; }
    public string? NumeroReferencia { get; set; }
    public DateOnly PeriodoInicio { get; set; }
    public DateOnly PeriodoFim { get; set; }
    public DateOnly? DataEnvio { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? AprovadorId { get; set; }
    public Usuario? Aprovador { get; set; }
    public string? ObservacaoAprovador { get; set; }
    public DateTime? DataDecisao { get; set; }
    public decimal ValorTotalMedido { get; set; }
    public string? Observacao { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }

    public List<MedicaoBmItem> Itens { get; set; } = [];
    public List<MedicaoBmAcerto> Acertos { get; set; } = [];
    public List<MedicaoBmImposto> Impostos { get; set; } = [];
}

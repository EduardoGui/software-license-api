namespace SoftwareLicense.Api.Entities;

public class Aditivo
{
    public int Id { get; set; }
    public int ContratoId { get; set; }
    public Contrato Contrato { get; set; } = null!;
    public int Numero { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public DateOnly DataAssinatura { get; set; }
    public DateOnly DataEfeito { get; set; }
    public decimal? DeltaValor { get; set; }
    public DateOnly? NovaDataFimVigencia { get; set; }
    public decimal? PercentualReajuste { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? DataFormalizacao { get; set; }
    public string? Observacao { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }

    public List<AditivoItem> Itens { get; set; } = [];
}

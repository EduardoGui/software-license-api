namespace SoftwareLicense.Api.Entities;

public class NotaFiscalItem
{
    public int Id { get; set; }
    public int NotaFiscalEntradaId { get; set; }
    public NotaFiscalEntrada NotaFiscalEntrada { get; set; } = null!;
    public int TipoEquipamentoId { get; set; }
    public TipoEquipamento TipoEquipamento { get; set; } = null!;
    public string? Descricao { get; set; }
    public int Quantidade { get; set; }
    public decimal? ValorUnitario { get; set; }
    public DateTime DataCriacao { get; set; }

    public ICollection<Equipamento> Equipamentos { get; set; } = new List<Equipamento>();
}

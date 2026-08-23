namespace SoftwareLicense.Api.Entities;

public class NotaFiscalItem
{
    public int Id { get; set; }
    public int NotaFiscalEntradaId { get; set; }
    public NotaFiscalEntrada NotaFiscalEntrada { get; set; } = null!;
    public string Destino { get; set; } = string.Empty;
    public int? TipoEquipamentoId { get; set; }
    public TipoEquipamento? TipoEquipamento { get; set; }
    public int? TipoPatrimonioId { get; set; }
    public TipoPatrimonio? TipoPatrimonio { get; set; }
    public int? LocalId { get; set; }
    public Local? Local { get; set; }
    public string? Descricao { get; set; }
    public int Quantidade { get; set; }
    public decimal? ValorUnitario { get; set; }
    public string Origem { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }

    public ICollection<Equipamento> Equipamentos { get; set; } = new List<Equipamento>();
    public ICollection<PatrimonioItem> PatrimonioItens { get; set; } = new List<PatrimonioItem>();
}

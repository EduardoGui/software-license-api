namespace SoftwareLicense.Api.Entities;

public class Equipamento
{
    public int Id { get; set; }
    public int TipoEquipamentoId { get; set; }
    public TipoEquipamento TipoEquipamento { get; set; } = null!;
    public int? NotaFiscalItemId { get; set; }
    public NotaFiscalItem? NotaFiscalItem { get; set; }
    public string? Marca { get; set; }
    public string? Modelo { get; set; }
    public string? NumeroSerie { get; set; }
    public string? Patrimonio { get; set; }
    public string Origem { get; set; } = string.Empty;
    public string? FornecedorNome { get; set; }
    public decimal? ValorMensal { get; set; }
    public DateOnly? DataInicioContrato { get; set; }
    public DateOnly? DataFimContrato { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateOnly? DataBaixa { get; set; }
    public string? NumeroNotaSaida { get; set; }
    public string? Observacao { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}

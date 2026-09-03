namespace SoftwareLicense.Api.DTOs;

public class MedicaoBmItemDto
{
    public int Id { get; set; }
    public int? ContratoItemId { get; set; }
    public int? AditivoItemId { get; set; }
    public string DescricaoNoMomento { get; set; } = string.Empty;
    public string UnidadeNoMomento { get; set; } = string.Empty;
    public decimal QuantidadeContratadaNoMomento { get; set; }
    public decimal QuantidadeJaMedidaAntes { get; set; }
    public decimal SaldoAntes { get; set; }
    public decimal QuantidadeMedidaNestaBm { get; set; }
    public decimal SaldoDepois { get; set; }
    public decimal ValorUnitarioNoMomento { get; set; }
    public decimal ValorTotalItem { get; set; }
    public decimal SaldoValorAntes { get; set; }
    public decimal SaldoValorDepois { get; set; }
    public DateOnly? InicioEfetivo { get; set; }
    public DateOnly? FimEfetivo { get; set; }
    public int? DiasBase { get; set; }
    public int? DiasMedidos { get; set; }
    public decimal? PercentualProRata { get; set; }
    public decimal? AjusteManual { get; set; }
    public string? JustificativaAjuste { get; set; }
}

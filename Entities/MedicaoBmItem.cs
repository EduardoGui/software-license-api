namespace SoftwareLicense.Api.Entities;

public class MedicaoBmItem
{
    public int Id { get; set; }
    public int MedicaoBmId { get; set; }
    public MedicaoBm MedicaoBm { get; set; } = null!;
    public int? ContratoItemId { get; set; }
    public ContratoItem? ContratoItem { get; set; }
    public int? AditivoItemId { get; set; }
    public AditivoItem? AditivoItem { get; set; }
    public string DescricaoNoMomento { get; set; } = string.Empty;
    public string UnidadeNoMomento { get; set; } = string.Empty;
    public decimal QuantidadeContratadaNoMomento { get; set; }
    public decimal QuantidadeJaMedidaAntes { get; set; }
    public decimal SaldoAntes { get; set; }
    public decimal QuantidadeMedidaNestaBm { get; set; }
    public decimal SaldoDepois { get; set; }
    public decimal ValorUnitarioNoMomento { get; set; }
    public decimal ValorTotalItem { get; set; }

    // Saldo monetário corrido — nunca recalculado como quantidade×preço; é a prática real da
    // empresa (subtrai o valor medido, já arredondado, do saldo anterior a cada BM sucessivo).
    public decimal SaldoValorAntes { get; set; }
    public decimal SaldoValorDepois { get; set; }

    // Pró-rata — só preenchido quando o item não cobre o período cheio.
    public DateOnly? PeriodoOriginalInicio { get; set; }
    public DateOnly? PeriodoOriginalFim { get; set; }
    public DateOnly? InicioEfetivo { get; set; }
    public DateOnly? FimEfetivo { get; set; }
    public int? DiasBase { get; set; }
    public int? DiasMedidos { get; set; }
    public decimal? PercentualProRata { get; set; }
    public decimal? AjusteManual { get; set; }
    public string? JustificativaAjuste { get; set; }
}

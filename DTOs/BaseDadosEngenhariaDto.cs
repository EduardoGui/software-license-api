namespace SoftwareLicense.Api.DTOs;

public class BaseDadosEngenhariaFiltroDto
{
    // Aplicados à competência do BM (PeriodoInicio).
    public DateOnly? De { get; set; }
    public DateOnly? Ate { get; set; }
    public string? Status { get; set; }
}

public class BaseDadosEngenhariaDto
{
    public List<BaseDadosEngenhariaLinhaDto> Linhas { get; set; } = [];
}

// Uma linha por (BM x item medido x UA do rateio). Os campos do BM e da NF se repetem em todas as
// linhas do mesmo BM; item/UA/NF ficam nulos quando não existem (o BM aparece mesmo assim).
public class BaseDadosEngenhariaLinhaDto
{
    public int MedicaoBmId { get; set; }
    public string ContratoNumero { get; set; } = string.Empty;
    public int NumeroBm { get; set; }
    public string? NumeroReferencia { get; set; }
    public DateOnly PeriodoInicio { get; set; }
    public DateOnly PeriodoFim { get; set; }
    public string Status { get; set; } = string.Empty;
    public string FornecedorNome { get; set; } = string.Empty;
    public string? FornecedorDocumento { get; set; }
    public decimal ValorTotalBm { get; set; }
    public decimal ValorLiquidoBm { get; set; }

    public string? ItemDescricao { get; set; }
    public string? ItemUnidade { get; set; }
    public decimal? ValorUnitario { get; set; }

    public string? CodigoUa { get; set; }
    public decimal? QuantidadeUa { get; set; }
    public decimal? ValorUa { get; set; }

    public DateOnly? NfDataEmissao { get; set; }
    public string? NfNumero { get; set; }
    public decimal? NfValorTotal { get; set; }
}

namespace SoftwareLicense.Api.DTOs;

public class BaseDadosEngenhariaFiltroDto
{
    // BM: competência (PeriodoInicio). Ordem de Compra: data da OC.
    public DateOnly? De { get; set; }
    public DateOnly? Ate { get; set; }
    public string? Status { get; set; }

    // "Medicao" ou "OrdemCompra"; vazio = ambas.
    public string? Origem { get; set; }
}

public class BaseDadosEngenhariaDto
{
    public List<BaseDadosEngenhariaLinhaDto> Linhas { get; set; } = [];
}

// Uma linha por (documento x item x UA do rateio). Os campos do documento (BM ou OC) e da NF se repetem em
// todas as linhas do mesmo documento; item/UA/NF ficam nulos quando não existem (o documento aparece mesmo assim).
public class BaseDadosEngenhariaLinhaDto
{
    public string Origem { get; set; } = string.Empty;
    public int DocumentoId { get; set; }
    // Só BM.
    public string? ContratoNumero { get; set; }
    // Nº do BM (dentro do contrato) ou nº da OC.
    public int NumeroDocumento { get; set; }
    public string? NumeroReferencia { get; set; }
    // BM: início da competência. OC: data da OC (PeriodoFim fica nulo).
    public DateOnly PeriodoInicio { get; set; }
    public DateOnly? PeriodoFim { get; set; }
    public string Status { get; set; } = string.Empty;
    public string FornecedorNome { get; set; } = string.Empty;
    public string? FornecedorDocumento { get; set; }
    public decimal ValorTotalBm { get; set; }
    // Só BM (bruto + acertos - impostos); OC não tem acertos/impostos.
    public decimal? ValorLiquidoBm { get; set; }

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

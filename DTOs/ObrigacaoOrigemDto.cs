namespace SoftwareLicense.Api.DTOs;

// Resumo somente leitura do documento de origem da obrigação (BM ou OC), para consulta sem sair da tela de obrigações.
public class ObrigacaoOrigemDto
{
    // MedicaoBm | OrdemCompra
    public string Origem { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string FornecedorNome { get; set; } = string.Empty;
    public int QuantidadeItens { get; set; }
    public string? Observacao { get; set; }

    // Valor principal do documento: líquido do BM (medido + acertos − impostos) ou total dos itens da OC.
    public decimal ValorPrincipal { get; set; }

    // --- BM ---
    public int? ContratoId { get; set; }
    public string? ContratoNumero { get; set; }
    public string? ContratoObjeto { get; set; }
    public int? MedicaoBmId { get; set; }
    public DateOnly? PeriodoInicio { get; set; }
    public DateOnly? PeriodoFim { get; set; }
    public DateOnly? DataEmissao { get; set; }
    public string? AprovadorNome { get; set; }
    public DateTime? DataDecisao { get; set; }
    public string? ObservacaoAprovador { get; set; }
    public decimal? ValorMedido { get; set; }
    public decimal? ValorAcertos { get; set; }
    public decimal? ValorImpostos { get; set; }

    // --- OC ---
    public int? OrdemCompraId { get; set; }
    public DateOnly? Data { get; set; }
    public string? Solicitante { get; set; }
    public string? LocalNome { get; set; }
    public string? CondicaoPagamento { get; set; }
    public decimal? ValorFrete { get; set; }
    public string? LocalEntrega { get; set; }
    public string? PrazoEntrega { get; set; }
}

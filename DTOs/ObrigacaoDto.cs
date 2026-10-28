namespace SoftwareLicense.Api.DTOs;

public class ObrigacaoDto
{
    public int Id { get; set; }
    public string TipoMovimento { get; set; } = string.Empty;
    public int? MedicaoBmId { get; set; }
    public int? MedicaoBmNumero { get; set; }
    public string? MedicaoBmNumeroReferencia { get; set; }
    public int? ContratoId { get; set; }
    public string? ContratoNumero { get; set; }
    public int? OrdemCompraId { get; set; }
    public int? OrdemCompraNumero { get; set; }
    public int? DespesaAvulsaId { get; set; }
    public string? DespesaAvulsaDescricao { get; set; }
    public int FornecedorId { get; set; }
    public string FornecedorNome { get; set; } = string.Empty;
    public DateOnly Competencia { get; set; }
    public decimal ValorPrevisto { get; set; }
    // Ordem de Compra paga em várias parcelas (uma NF, vários boletos): nº desta parcela e quantas parcelas ativas a OC tem.
    public int NumeroParcela { get; set; } = 1;
    public int TotalParcelas { get; set; } = 1;
    // Só quando a OC tem 2+ parcelas: soma dos valores previstos das parcelas ativas e o valor que ela deveria fechar
    // (valor da NF, se já informado; senão o total dos itens da OC).
    public decimal? SomaParcelas { get; set; }
    public decimal? ValorReferenciaParcelas { get; set; }
    public DateOnly? DataNf { get; set; }
    public string? NumeroNf { get; set; }
    public decimal? ValorNota { get; set; }
    public DateOnly? Vencimento { get; set; }
    public DateOnly? DataRequisicao { get; set; }
    public DateOnly? DataAssistPgto { get; set; }
    public DateOnly? DataEnvioFinanceiro { get; set; }
    public DateOnly? DataPrevistaPagamento { get; set; }
    public bool Pago { get; set; }
    public bool Cancelada { get; set; }
    public string Etapa { get; set; } = string.Empty;
    // Andamento documental da origem (OC ou BM) - igual para todas as parcelas de uma mesma OC.
    public DateOnly? DataSolicitacaoEspelho { get; set; }
    public DateOnly? DataEnvioAssinatura { get; set; }
    public DateOnly? DataEnvioFornecedor { get; set; }
    public DateOnly? DataChegadaMaterial { get; set; }
    public DateOnly? DataLiberacaoNf { get; set; }
    // OC: sempre true. Medição: vem da configuração do contrato. Despesa avulsa: false.
    public bool ExigeAssinatura { get; set; }
    public bool OrdemCompraAssinada { get; set; }
    // Quem criou a OC/despesa de origem (nulo em medição e em registros antigos).
    public string? CriadoPorNome { get; set; }
    public string? Observacoes { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataAtualizacao { get; set; }
}

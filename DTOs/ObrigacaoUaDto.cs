using System.ComponentModel.DataAnnotations;

namespace SoftwareLicense.Api.DTOs;

// UAs (unidades orçamentárias) de uma obrigação, vindas do documento de origem (BM, OC ou despesa avulsa).
public class ObrigacaoUaDto
{
    // MedicaoBm | OrdemCompra | DespesaAvulsa
    public string Origem { get; set; } = string.Empty;
    // 'quantidade' (itens de BM/OC) ou 'valor' (despesa avulsa, rateio em R$).
    public string Modo { get; set; } = "quantidade";
    public bool PodeEditar { get; set; }
    public string? MotivoBloqueio { get; set; }
    // Total por UA, somando os itens (em R$), para exibir na linha da obrigação.
    public List<ObrigacaoUaResumoDto> Resumo { get; set; } = [];
    public List<ObrigacaoUaItemDto> Itens { get; set; } = [];
}

public class ObrigacaoUaResumoDto
{
    public int UnidadeOrcamentariaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
}

// Um item rateável: item da BM, item da OC ou a própria despesa avulsa (ItemId nulo).
public class ObrigacaoUaItemDto
{
    public int? ItemId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string? Unidade { get; set; }
    // Quantidade do item (modo 'quantidade') ou valor da despesa (modo 'valor'): o rateio precisa fechar com ela.
    public decimal Total { get; set; }
    public decimal ValorTotal { get; set; }
    public List<ObrigacaoUaLinhaDto> Linhas { get; set; } = [];
}

public class ObrigacaoUaLinhaDto
{
    public int UnidadeOrcamentariaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public decimal Quantidade { get; set; }
}

public class DefinirUaObrigacaoDto
{
    // Nulo na despesa avulsa (rateio da despesa inteira); id do item da BM/OC nos demais casos.
    public int? ItemId { get; set; }

    [Required(ErrorMessage = "Linhas é obrigatório.")]
    [MinLength(1, ErrorMessage = "Informe ao menos uma UA para o rateio.")]
    public List<ItemRateioUaInputDto> Linhas { get; set; } = [];
}

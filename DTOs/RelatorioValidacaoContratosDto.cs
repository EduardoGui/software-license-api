namespace SoftwareLicense.Api.DTOs;

// Relatório para validação com o gerente do contrato: dados fixos, itens, saldo, aditivos e medições (BM).
// Só leitura. Sem lista de contratos informada, traz os contratos com tarefa de medição pendente na Agenda.
public class RelatorioValidacaoContratosDto
{
    public DateTime GeradoEm { get; set; }
    public bool SomentePendentes { get; set; }
    public List<RelatorioValidacaoContratoDto> Contratos { get; set; } = [];
}

public class RelatorioValidacaoContratoDto
{
    public int ContratoId { get; set; }
    public string Numero { get; set; } = string.Empty;
    public string FornecedorNome { get; set; } = string.Empty;
    public string Objeto { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateOnly DataAssinatura { get; set; }
    public DateOnly DataInicioVigencia { get; set; }
    public DateOnly DataFimVigenciaOriginal { get; set; }
    public DateOnly DataFimVigenciaAtual { get; set; }
    public decimal ValorOriginal { get; set; }
    public decimal ValorAtual { get; set; }

    // Tarefa de medição pendente na Agenda (a mais antiga), quando houver.
    public string? PendenciaTitulo { get; set; }
    public DateOnly? PendenciaDataPrevista { get; set; }
    public int? PendenciaDiasParaVencer { get; set; }

    public List<ContratoItemDto> Itens { get; set; } = [];
    public List<ContratoSaldoItemDto> Saldo { get; set; } = [];
    public List<RelatorioValidacaoAditivoDto> Aditivos { get; set; } = [];
    public List<RelatorioValidacaoMedicaoDto> Medicoes { get; set; } = [];
}

public class RelatorioValidacaoAditivoDto
{
    public int Numero { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public DateOnly DataAssinatura { get; set; }
    public DateOnly DataEfeito { get; set; }
    public decimal? DeltaValor { get; set; }
    public DateOnly? NovaDataFimVigencia { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class RelatorioValidacaoMedicaoDto
{
    public int Numero { get; set; }
    public string? NumeroReferencia { get; set; }
    public DateOnly PeriodoInicio { get; set; }
    public DateOnly PeriodoFim { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal ValorTotalMedido { get; set; }
}

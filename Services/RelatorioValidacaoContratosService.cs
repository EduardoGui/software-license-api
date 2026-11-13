using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public class RelatorioValidacaoContratosService : IRelatorioValidacaoContratosService
{
    private const string FormatoData = "dd/MM/yyyy";
    private const string FormatoValor = "#,##0.00";
    private const string FormatoQuantidade = "#,##0.####";
    private const string NomeResumo = "Resumo";

    private static readonly XLColor CorTitulo = XLColor.FromHtml("#27394F");
    private static readonly XLColor CorCabecalho = XLColor.FromHtml("#E9EBED");

    private readonly IContratoService _contratoService;
    private readonly ITarefaOcorrenciaService _tarefaOcorrenciaService;
    private readonly TimeProvider _timeProvider;

    public RelatorioValidacaoContratosService(
        IContratoService contratoService,
        ITarefaOcorrenciaService tarefaOcorrenciaService,
        TimeProvider timeProvider)
    {
        _contratoService = contratoService;
        _tarefaOcorrenciaService = tarefaOcorrenciaService;
        _timeProvider = timeProvider;
    }

    public async Task<RelatorioValidacaoContratosDto> GerarAsync(IReadOnlyCollection<int>? contratoIds)
    {
        // Mesmas tarefas que a Agenda mostra hoje (a consulta também materializa os lembretes de medição).
        var pendencias = (await _tarefaOcorrenciaService.ObterAgendaAsync())
            .Where(o => o.ContratoId != null)
            .GroupBy(o => o.ContratoId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(o => o.DataPrevistaAtual).First());

        var somentePendentes = contratoIds is null || contratoIds.Count == 0;
        var ids = somentePendentes ? pendencias.Keys.ToList() : contratoIds!.Distinct().ToList();

        var contratos = new List<RelatorioValidacaoContratoDto>();
        foreach (var id in ids)
        {
            var detalhe = await _contratoService.GetByIdAsync(id);
            var saldo = await _contratoService.ObterSaldoAsync(id);
            var aditivos = await _contratoService.ListarAditivosAsync(id);
            var medicoes = await _contratoService.ListarMedicoesAsync(id);
            pendencias.TryGetValue(id, out var pendencia);

            contratos.Add(new RelatorioValidacaoContratoDto
            {
                ContratoId = detalhe.Id,
                Numero = detalhe.Numero,
                FornecedorNome = detalhe.FornecedorNome,
                Objeto = detalhe.Objeto,
                Status = detalhe.Status,
                DataAssinatura = detalhe.DataAssinatura,
                DataInicioVigencia = detalhe.DataInicioVigencia,
                DataFimVigenciaOriginal = detalhe.DataFimVigenciaOriginal,
                DataFimVigenciaAtual = detalhe.DataFimVigenciaAtual,
                ValorOriginal = detalhe.ValorOriginal,
                ValorAtual = detalhe.ValorAtual,
                PendenciaTitulo = pendencia?.Titulo,
                PendenciaDataPrevista = pendencia?.DataPrevistaAtual,
                PendenciaDiasParaVencer = pendencia?.DiasParaVencer,
                Itens = detalhe.Itens,
                Saldo = saldo,
                Aditivos = aditivos
                    .OrderBy(a => a.Numero)
                    .Select(a => new RelatorioValidacaoAditivoDto
                    {
                        Numero = a.Numero,
                        Descricao = a.Descricao,
                        DataAssinatura = a.DataAssinatura,
                        DataEfeito = a.DataEfeito,
                        DeltaValor = a.DeltaValor,
                        NovaDataFimVigencia = a.NovaDataFimVigencia,
                        Status = a.Status,
                    })
                    .ToList(),
                Medicoes = medicoes
                    .OrderBy(m => m.Numero)
                    .Select(m => new RelatorioValidacaoMedicaoDto
                    {
                        Numero = m.Numero,
                        NumeroReferencia = m.NumeroReferencia,
                        PeriodoInicio = m.PeriodoInicio,
                        PeriodoFim = m.PeriodoFim,
                        Status = m.Status,
                        ValorTotalMedido = m.ValorTotalMedido,
                    })
                    .ToList(),
            });
        }

        return new RelatorioValidacaoContratosDto
        {
            GeradoEm = _timeProvider.GetLocalNow().DateTime,
            SomentePendentes = somentePendentes,
            Contratos = contratos
                .OrderBy(c => c.FornecedorNome, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.Numero, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };
    }

    public byte[] GerarExcel(RelatorioValidacaoContratosDto relatorio)
    {
        using var workbook = new XLWorkbook();
        var nomesUsados = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { NomeResumo };

        MontarResumo(workbook.Worksheets.Add(NomeResumo), relatorio);

        foreach (var contrato in relatorio.Contratos)
        {
            MontarContrato(workbook.Worksheets.Add(NomePlanilha(contrato.Numero, nomesUsados)), contrato);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void MontarResumo(IXLWorksheet planilha, RelatorioValidacaoContratosDto relatorio)
    {
        planilha.Cell(1, 1).Value = "Relatório para validação de medições";
        planilha.Cell(1, 1).Style.Font.Bold = true;
        planilha.Cell(1, 1).Style.Font.FontSize = 14;
        planilha.Cell(2, 1).Value = $"Gerado em {relatorio.GeradoEm.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)}"
            + (relatorio.SomentePendentes ? " — contratos com medição pendente na Agenda" : string.Empty);

        string[] cabecalhos = ["Fornecedor", "Contrato", "Pendência na Agenda", "Data prevista", "Situação", "Valor atual", "Vigência até", "BMs lançados"];
        const int linhaCabecalho = 4;
        EscreverCabecalho(planilha, linhaCabecalho, cabecalhos);

        var linha = linhaCabecalho + 1;
        if (relatorio.Contratos.Count == 0)
        {
            planilha.Cell(linha, 1).Value = "Nenhum contrato com medição pendente.";
        }

        foreach (var c in relatorio.Contratos)
        {
            planilha.Cell(linha, 1).Value = c.FornecedorNome;
            planilha.Cell(linha, 2).Value = c.Numero;
            planilha.Cell(linha, 3).Value = c.PendenciaTitulo ?? "-";
            Data(planilha.Cell(linha, 4), c.PendenciaDataPrevista);
            planilha.Cell(linha, 5).Value = SituacaoPendencia(c.PendenciaDiasParaVencer);
            Valor(planilha.Cell(linha, 6), c.ValorAtual);
            Data(planilha.Cell(linha, 7), c.DataFimVigenciaAtual);
            planilha.Cell(linha, 8).Value = c.Medicoes.Count;
            linha++;
        }

        planilha.Column(1).Width = 42;
        planilha.Column(2).Width = 22;
        planilha.Column(3).Width = 48;
        planilha.Column(4).Width = 16;
        planilha.Column(5).Width = 22;
        planilha.Column(6).Width = 18;
        planilha.Column(7).Width = 16;
        planilha.Column(8).Width = 16;
    }

    private static void MontarContrato(IXLWorksheet planilha, RelatorioValidacaoContratoDto c)
    {
        planilha.Column(1).Width = 28;
        planilha.Column(2).Width = 60;
        planilha.Column(3).Width = 16;
        planilha.Column(4).Width = 18;
        planilha.Column(5).Width = 18;
        planilha.Column(6).Width = 18;
        planilha.Column(7).Width = 20;
        planilha.Column(8).Width = 18;

        planilha.Cell(1, 1).Value = c.FornecedorNome;
        planilha.Cell(1, 1).Style.Font.Bold = true;
        planilha.Cell(1, 1).Style.Font.FontSize = 14;
        planilha.Cell(2, 1).Value = $"Contrato {c.Numero}";
        if (c.PendenciaTitulo is not null)
        {
            planilha.Cell(3, 1).Value = $"Pendência: {c.PendenciaTitulo}"
                + (c.PendenciaDataPrevista is { } prevista ? $" — prevista para {prevista.ToString(FormatoData, CultureInfo.InvariantCulture)}" : string.Empty);
        }

        var linha = 5;

        // Dados fixos
        linha = Secao(planilha, linha, "Dados fixos");
        linha = Campo(planilha, linha, "Fornecedor", c.FornecedorNome);
        linha = Campo(planilha, linha, "Número do contrato", c.Numero);
        linha = Campo(planilha, linha, "Objeto", c.Objeto, quebrar: true);
        linha = Campo(planilha, linha, "Situação", c.Status);
        linha = CampoData(planilha, linha, "Data de assinatura", c.DataAssinatura);
        linha = CampoData(planilha, linha, "Início da vigência", c.DataInicioVigencia);
        linha = CampoData(planilha, linha, "Fim da vigência (original)", c.DataFimVigenciaOriginal);
        linha = CampoData(planilha, linha, "Fim da vigência (atual)", c.DataFimVigenciaAtual);
        linha = CampoValor(planilha, linha, "Valor original", c.ValorOriginal);
        linha = CampoValor(planilha, linha, "Valor atual", c.ValorAtual);
        linha++;

        // Itens do contrato
        linha = Secao(planilha, linha, "Itens do contrato");
        EscreverCabecalho(planilha, linha++, ["Código", "Descrição", "Unidade", "Quantidade", "Valor unitário", "Valor total"]);
        if (c.Itens.Count == 0)
        {
            planilha.Cell(linha++, 1).Value = "Nenhum item cadastrado.";
        }

        foreach (var item in c.Itens)
        {
            planilha.Cell(linha, 1).Value = item.Codigo ?? "-";
            Texto(planilha.Cell(linha, 2), item.Descricao);
            planilha.Cell(linha, 3).Value = item.Unidade;
            Quantidade(planilha.Cell(linha, 4), item.QuantidadeContratada);
            Valor(planilha.Cell(linha, 5), item.ValorUnitario);
            Valor(planilha.Cell(linha, 6), item.ValorTotal);
            linha++;
        }

        linha++;

        // Saldo
        linha = Secao(planilha, linha, "Saldo do contrato");
        EscreverCabecalho(planilha, linha++, ["#", "Item", "Unidade", "Contratado (atual)", "Já medido (aprovado)", "Saldo (quantidade)", "Valor contratado (atual)", "Saldo (valor)"]);
        if (c.Saldo.Count == 0)
        {
            planilha.Cell(linha++, 1).Value = "Sem saldo a exibir.";
        }

        var numeroSaldo = 1;
        foreach (var s in c.Saldo)
        {
            planilha.Cell(linha, 1).Value = numeroSaldo++;
            Texto(planilha.Cell(linha, 2), s.Descricao);
            planilha.Cell(linha, 3).Value = s.Unidade;
            Quantidade(planilha.Cell(linha, 4), s.QuantidadeContratadaAtual);
            Quantidade(planilha.Cell(linha, 5), s.QuantidadeJaMedida);
            Quantidade(planilha.Cell(linha, 6), s.SaldoQuantidade);
            Valor(planilha.Cell(linha, 7), s.ValorContratadoAtual);
            Valor(planilha.Cell(linha, 8), s.SaldoValor);
            linha++;
        }

        linha++;

        // Aditivos
        linha = Secao(planilha, linha, "Aditivos");
        if (c.Aditivos.Count == 0)
        {
            planilha.Cell(linha++, 1).Value = "Nenhum aditivo.";
        }
        else
        {
            EscreverCabecalho(planilha, linha++, ["Nº", "Descrição", "Assinatura", "Efeito", "Variação de valor", "Nova vigência até", "Situação"]);
            foreach (var a in c.Aditivos)
            {
                planilha.Cell(linha, 1).Value = a.Numero;
                Texto(planilha.Cell(linha, 2), a.Descricao);
                Data(planilha.Cell(linha, 3), a.DataAssinatura);
                Data(planilha.Cell(linha, 4), a.DataEfeito);
                Valor(planilha.Cell(linha, 5), a.DeltaValor);
                Data(planilha.Cell(linha, 6), a.NovaDataFimVigencia);
                planilha.Cell(linha, 7).Value = a.Status;
                linha++;
            }
        }

        linha++;

        // Boletins de medição
        linha = Secao(planilha, linha, "Boletins de medição (BM)");
        if (c.Medicoes.Count == 0)
        {
            planilha.Cell(linha++, 1).Value = "Nenhuma medição lançada.";
        }
        else
        {
            EscreverCabecalho(planilha, linha++, ["Nº", "Referência", "Período (início)", "Período (fim)", "Situação", "Valor total medido"]);
            foreach (var m in c.Medicoes)
            {
                planilha.Cell(linha, 1).Value = m.Numero;
                planilha.Cell(linha, 2).Value = m.NumeroReferencia ?? "-";
                Data(planilha.Cell(linha, 3), m.PeriodoInicio);
                Data(planilha.Cell(linha, 4), m.PeriodoFim);
                planilha.Cell(linha, 5).Value = m.Status;
                Valor(planilha.Cell(linha, 6), m.ValorTotalMedido);
                linha++;
            }

            planilha.Cell(linha, 5).Value = "Total aprovado";
            planilha.Cell(linha, 5).Style.Font.Bold = true;
            Valor(planilha.Cell(linha, 6), c.Medicoes.Where(m => string.Equals(m.Status, "Aprovado", StringComparison.OrdinalIgnoreCase)).Sum(m => m.ValorTotalMedido));
            planilha.Cell(linha, 6).Style.Font.Bold = true;
        }
    }

    private static int Secao(IXLWorksheet planilha, int linha, string titulo)
    {
        var celula = planilha.Cell(linha, 1);
        celula.Value = titulo;
        celula.Style.Font.Bold = true;
        celula.Style.Font.FontSize = 12;
        celula.Style.Font.FontColor = CorTitulo;
        return linha + 1;
    }

    private static void EscreverCabecalho(IXLWorksheet planilha, int linha, string[] cabecalhos)
    {
        for (var coluna = 0; coluna < cabecalhos.Length; coluna++)
        {
            var celula = planilha.Cell(linha, coluna + 1);
            celula.Value = cabecalhos[coluna];
            celula.Style.Font.Bold = true;
            celula.Style.Fill.BackgroundColor = CorCabecalho;
            celula.Style.Alignment.WrapText = true;
        }
    }

    private static int Campo(IXLWorksheet planilha, int linha, string rotulo, string valor, bool quebrar = false)
    {
        planilha.Cell(linha, 1).Value = rotulo;
        planilha.Cell(linha, 1).Style.Font.Bold = true;
        planilha.Cell(linha, 2).Value = valor;
        planilha.Cell(linha, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        planilha.Cell(linha, 2).Style.Alignment.WrapText = quebrar;
        return linha + 1;
    }

    private static int CampoData(IXLWorksheet planilha, int linha, string rotulo, DateOnly valor)
    {
        planilha.Cell(linha, 1).Value = rotulo;
        planilha.Cell(linha, 1).Style.Font.Bold = true;
        Data(planilha.Cell(linha, 2), valor);
        planilha.Cell(linha, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        return linha + 1;
    }

    private static int CampoValor(IXLWorksheet planilha, int linha, string rotulo, decimal valor)
    {
        planilha.Cell(linha, 1).Value = rotulo;
        planilha.Cell(linha, 1).Style.Font.Bold = true;
        Valor(planilha.Cell(linha, 2), valor);
        planilha.Cell(linha, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        return linha + 1;
    }

    private static void Texto(IXLCell celula, string texto)
    {
        celula.Value = texto;
        celula.Style.Alignment.WrapText = true;
        celula.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
    }

    private static void Data(IXLCell celula, DateOnly? data)
    {
        if (data is null)
        {
            celula.Value = "-";
            celula.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            return;
        }

        celula.Value = data.Value.ToDateTime(TimeOnly.MinValue);
        celula.Style.DateFormat.Format = FormatoData;
    }

    private static void Valor(IXLCell celula, decimal? valor)
    {
        if (valor is null)
        {
            celula.Value = "-";
            celula.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            return;
        }

        celula.Value = valor.Value;
        celula.Style.NumberFormat.Format = FormatoValor;
    }

    private static void Quantidade(IXLCell celula, decimal valor)
    {
        celula.Value = valor;
        celula.Style.NumberFormat.Format = FormatoQuantidade;
    }

    private static string SituacaoPendencia(int? diasParaVencer) => diasParaVencer switch
    {
        null => "-",
        < 0 => $"Vencida há {-diasParaVencer} dia(s)",
        0 => "Vence hoje",
        _ => $"Vence em {diasParaVencer} dia(s)",
    };

    // Nome de aba do Excel: até 31 caracteres, sem : \ / ? * [ ], único sem diferenciar maiúsculas.
    private static string NomePlanilha(string numero, HashSet<string> usados)
    {
        var nome = Regex.Replace(numero, @"[:\\/?*\[\]]", "-").Trim();
        if (nome.Length == 0)
        {
            nome = "Contrato";
        }

        if (nome.Length > 31)
        {
            nome = nome[..31];
        }

        var original = nome;
        var contador = 2;
        while (!usados.Add(nome))
        {
            var sufixo = $" ({contador++})";
            nome = original[..Math.Min(original.Length, 31 - sufixo.Length)] + sufixo;
        }

        return nome;
    }
}

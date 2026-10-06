using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class BaseDadosEngenhariaService : IBaseDadosEngenhariaService
{
    private static readonly string[] StatusValidos =
    [
        MedicaoBmStatus.Rascunho, MedicaoBmStatus.AguardandoAprovacao, MedicaoBmStatus.Aprovado, MedicaoBmStatus.Reprovado,
        OrdemCompraStatus.Emitida, OrdemCompraStatus.Assinada, OrdemCompraStatus.Cancelada,
    ];

    private readonly AppDbContext _context;

    public BaseDadosEngenhariaService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<BaseDadosEngenhariaDto> GerarAsync(BaseDadosEngenhariaFiltroDto filtro)
    {
        if (filtro.De is not null && filtro.Ate is not null && filtro.Ate < filtro.De)
        {
            throw new BusinessRuleException("A data final não pode ser anterior à data inicial.");
        }

        if (!string.IsNullOrWhiteSpace(filtro.Status) && !StatusValidos.Contains(filtro.Status))
        {
            throw new BusinessRuleException("Status inválido.");
        }

        if (!string.IsNullOrWhiteSpace(filtro.Origem)
            && filtro.Origem != BaseDadosOrigem.Medicao && filtro.Origem != BaseDadosOrigem.OrdemCompra)
        {
            throw new BusinessRuleException("Origem inválida.");
        }

        var linhas = new List<BaseDadosEngenhariaLinhaDto>();

        if (string.IsNullOrWhiteSpace(filtro.Origem) || filtro.Origem == BaseDadosOrigem.Medicao)
        {
            linhas.AddRange(await GerarLinhasMedicoesAsync(filtro));
        }

        if (string.IsNullOrWhiteSpace(filtro.Origem) || filtro.Origem == BaseDadosOrigem.OrdemCompra)
        {
            linhas.AddRange(await GerarLinhasOrdensCompraAsync(filtro));
        }

        return new BaseDadosEngenhariaDto { Linhas = linhas };
    }

    private async Task<List<BaseDadosEngenhariaLinhaDto>> GerarLinhasMedicoesAsync(BaseDadosEngenhariaFiltroDto filtro)
    {
        var query = _context.MedicaoBms.AsNoTracking().AsQueryable();

        if (filtro.De is not null)
        {
            query = query.Where(m => m.PeriodoInicio >= filtro.De);
        }

        if (filtro.Ate is not null)
        {
            query = query.Where(m => m.PeriodoInicio <= filtro.Ate);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Status))
        {
            query = query.Where(m => m.Status == filtro.Status);
        }

        var medicoes = await query
            .Include(m => m.Contrato).ThenInclude(c => c.Fornecedor)
            .Include(m => m.Acertos)
            .Include(m => m.Impostos)
            .Include(m => m.Itens).ThenInclude(i => i.RateiosUa).ThenInclude(r => r.UnidadeOrcamentaria)
            .AsSplitQuery()
            .OrderBy(m => m.Contrato.Numero).ThenBy(m => m.Numero)
            .ToListAsync();

        var ids = medicoes.Select(m => m.Id).ToList();
        // A NF do BM é a Obrigação ligada a ele (preenchida em Controle de Obrigações); cancelada = sem NF.
        var notas = await _context.Obrigacoes.AsNoTracking()
            .Where(o => o.MedicaoBmId != null && ids.Contains(o.MedicaoBmId.Value) && !o.Cancelada)
            .ToDictionaryAsync(o => o.MedicaoBmId!.Value);

        var linhas = new List<BaseDadosEngenhariaLinhaDto>();
        foreach (var medicao in medicoes)
        {
            var fornecedor = medicao.Contrato.Fornecedor;
            notas.TryGetValue(medicao.Id, out var nota);

            BaseDadosEngenhariaLinhaDto NovaLinha() => new()
            {
                Origem = BaseDadosOrigem.Medicao,
                DocumentoId = medicao.Id,
                ContratoNumero = medicao.Contrato.Numero,
                NumeroDocumento = medicao.Numero,
                NumeroReferencia = medicao.NumeroReferencia,
                PeriodoInicio = medicao.PeriodoInicio,
                PeriodoFim = medicao.PeriodoFim,
                Status = medicao.Status,
                FornecedorNome = fornecedor.Nome,
                FornecedorDocumento = DocumentoDoFornecedor(fornecedor),
                ValorTotalBm = medicao.ValorTotalMedido,
                ValorLiquidoBm = medicao.ValorTotalMedido + medicao.Acertos.Sum(a => a.PrecoTotal) - medicao.Impostos.Sum(i => i.ValorTotal),
                NfDataEmissao = nota?.DataNf,
                NfNumero = nota?.NumeroNf,
                NfValorTotal = nota?.ValorNota,
            };

            var itensMedidos = medicao.Itens
                .Where(i => i.QuantidadeMedidaNestaBm > 0)
                .OrderBy(i => i.Id)
                .Select(i => (i.DescricaoNoMomento, i.UnidadeNoMomento, i.ValorUnitarioNoMomento, Rateios: RateiosDoItem(i.RateiosUa)))
                .ToList();

            AdicionarLinhasDosItens(linhas, NovaLinha, itensMedidos);
        }

        return linhas;
    }

    private async Task<List<BaseDadosEngenhariaLinhaDto>> GerarLinhasOrdensCompraAsync(BaseDadosEngenhariaFiltroDto filtro)
    {
        var query = _context.OrdensCompra.AsNoTracking().AsQueryable();

        if (filtro.De is not null)
        {
            query = query.Where(o => o.Data >= filtro.De);
        }

        if (filtro.Ate is not null)
        {
            query = query.Where(o => o.Data <= filtro.Ate);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Status))
        {
            query = query.Where(o => o.Status == filtro.Status);
        }

        var ordens = await query
            .Include(o => o.Fornecedor)
            .Include(o => o.Itens).ThenInclude(i => i.RateiosUa).ThenInclude(r => r.UnidadeOrcamentaria)
            .AsSplitQuery()
            .OrderBy(o => o.Data).ThenBy(o => o.Numero)
            .ToListAsync();

        var ids = ordens.Select(o => o.Id).ToList();
        // A NF da OC é a Obrigação ligada a ela; OCs antigas podem não ter obrigação (NF fica vazia).
        var notas = await _context.Obrigacoes.AsNoTracking()
            .Where(o => o.OrdemCompraId != null && ids.Contains(o.OrdemCompraId.Value) && !o.Cancelada)
            .ToDictionaryAsync(o => o.OrdemCompraId!.Value);

        var linhas = new List<BaseDadosEngenhariaLinhaDto>();
        foreach (var ordem in ordens)
        {
            notas.TryGetValue(ordem.Id, out var nota);

            BaseDadosEngenhariaLinhaDto NovaLinha() => new()
            {
                Origem = BaseDadosOrigem.OrdemCompra,
                DocumentoId = ordem.Id,
                NumeroDocumento = ordem.Numero,
                PeriodoInicio = ordem.Data,
                Status = ordem.Status,
                FornecedorNome = ordem.Fornecedor.Nome,
                FornecedorDocumento = DocumentoDoFornecedor(ordem.Fornecedor),
                // O frete não entra: a obrigação da OC também considera só os itens.
                ValorTotalBm = ordem.Itens.Sum(i => i.Quantidade * i.ValorUnitario),
                NfDataEmissao = nota?.DataNf,
                NfNumero = nota?.NumeroNf,
                NfValorTotal = nota?.ValorNota,
            };

            var itens = ordem.Itens
                .OrderBy(i => i.Id)
                .Select(i => (i.Descricao, i.Unidade, i.ValorUnitario, Rateios: RateiosDoItem(i.RateiosUa)))
                .ToList();

            AdicionarLinhasDosItens(linhas, NovaLinha, itens);
        }

        return linhas;
    }

    private static string? DocumentoDoFornecedor(Fornecedor fornecedor) =>
        !string.IsNullOrWhiteSpace(fornecedor.Cnpj) ? fornecedor.Cnpj : fornecedor.Cpf;

    private static List<(string Codigo, decimal Quantidade)> RateiosDoItem(IEnumerable<MedicaoBmItemRateioUa> rateios) =>
        rateios.Where(r => r.Quantidade > 0)
            .OrderBy(r => r.UnidadeOrcamentaria.Codigo)
            .Select(r => (r.UnidadeOrcamentaria.Codigo, r.Quantidade))
            .ToList();

    private static List<(string Codigo, decimal Quantidade)> RateiosDoItem(IEnumerable<OrdemCompraItemRateioUa> rateios) =>
        rateios.Where(r => r.Quantidade > 0)
            .OrderBy(r => r.UnidadeOrcamentaria.Codigo)
            .Select(r => (r.UnidadeOrcamentaria.Codigo, r.Quantidade))
            .ToList();

    // Documento sem itens (ou BM sem item medido) = 1 linha só com o cabeçalho; item sem rateio = linha com UA vazia;
    // item rateado = 1 linha por UA com qtde x valor unitário.
    private static void AdicionarLinhasDosItens(
        List<BaseDadosEngenhariaLinhaDto> linhas,
        Func<BaseDadosEngenhariaLinhaDto> novaLinha,
        List<(string Descricao, string Unidade, decimal ValorUnitario, List<(string Codigo, decimal Quantidade)> Rateios)> itens)
    {
        if (itens.Count == 0)
        {
            linhas.Add(novaLinha());
            return;
        }

        foreach (var item in itens)
        {
            BaseDadosEngenhariaLinhaDto LinhaDoItem()
            {
                var linha = novaLinha();
                linha.ItemDescricao = item.Descricao;
                linha.ItemUnidade = item.Unidade;
                linha.ValorUnitario = item.ValorUnitario;
                return linha;
            }

            if (item.Rateios.Count == 0)
            {
                linhas.Add(LinhaDoItem());
                continue;
            }

            foreach (var rateio in item.Rateios)
            {
                var linha = LinhaDoItem();
                linha.CodigoUa = rateio.Codigo;
                linha.QuantidadeUa = rateio.Quantidade;
                linha.ValorUa = Math.Round(rateio.Quantidade * item.ValorUnitario, 2, MidpointRounding.AwayFromZero);
                linhas.Add(linha);
            }
        }
    }

    public byte[] GerarExcel(BaseDadosEngenhariaDto relatorio)
    {
        using var workbook = new XLWorkbook();
        var planilha = workbook.Worksheets.Add("Base de dados");

        string[] cabecalhos =
        [
            "Origem", "Contrato", "Nº documento", "Referência", "Data / competência início", "Competência fim", "Status",
            "Fornecedor", "CNPJ/CPF", "Valor total", "Valor líquido", "Item", "Unidade", "Valor unitário", "Código UA",
            "Qtde UA", "Qtde UA x Valor unit.", "NF - Data emissão", "NF - Número", "NF - Valor total",
        ];
        for (var coluna = 0; coluna < cabecalhos.Length; coluna++)
        {
            planilha.Cell(1, coluna + 1).Value = cabecalhos[coluna];
        }
        planilha.Row(1).Style.Font.Bold = true;

        var linha = 2;
        foreach (var l in relatorio.Linhas)
        {
            planilha.Cell(linha, 1).Value = RotuloOrigem(l.Origem);
            planilha.Cell(linha, 2).Value = l.ContratoNumero ?? string.Empty;
            planilha.Cell(linha, 3).Value = l.NumeroDocumento;
            planilha.Cell(linha, 4).Value = l.NumeroReferencia ?? string.Empty;
            EscreverData(planilha.Cell(linha, 5), l.PeriodoInicio);
            EscreverData(planilha.Cell(linha, 6), l.PeriodoFim);
            planilha.Cell(linha, 7).Value = RotuloStatus(l.Status);
            planilha.Cell(linha, 8).Value = l.FornecedorNome;
            planilha.Cell(linha, 9).Value = l.FornecedorDocumento ?? string.Empty;
            EscreverValor(planilha.Cell(linha, 10), l.ValorTotalBm);
            EscreverValor(planilha.Cell(linha, 11), l.ValorLiquidoBm);
            planilha.Cell(linha, 12).Value = l.ItemDescricao ?? string.Empty;
            planilha.Cell(linha, 13).Value = l.ItemUnidade ?? string.Empty;
            EscreverValor(planilha.Cell(linha, 14), l.ValorUnitario);
            planilha.Cell(linha, 15).Value = l.CodigoUa ?? string.Empty;
            EscreverQuantidade(planilha.Cell(linha, 16), l.QuantidadeUa);
            EscreverValor(planilha.Cell(linha, 17), l.ValorUa);
            EscreverData(planilha.Cell(linha, 18), l.NfDataEmissao);
            planilha.Cell(linha, 19).Value = l.NfNumero ?? string.Empty;
            EscreverValor(planilha.Cell(linha, 20), l.NfValorTotal);
            linha++;
        }

        planilha.SheetView.FreezeRows(1);
        planilha.Range(1, 1, Math.Max(linha - 1, 1), cabecalhos.Length).SetAutoFilter();
        planilha.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void EscreverData(IXLCell celula, DateOnly? data)
    {
        if (data is null)
        {
            return;
        }

        celula.Value = data.Value.ToDateTime(TimeOnly.MinValue);
        celula.Style.DateFormat.Format = "dd/mm/yyyy";
    }

    private static void EscreverValor(IXLCell celula, decimal? valor)
    {
        if (valor is null)
        {
            return;
        }

        celula.Value = valor.Value;
        celula.Style.NumberFormat.Format = "#,##0.00";
    }

    private static void EscreverQuantidade(IXLCell celula, decimal? quantidade)
    {
        if (quantidade is null)
        {
            return;
        }

        celula.Value = quantidade.Value;
        celula.Style.NumberFormat.Format = "#,##0.######";
    }

    private static string RotuloOrigem(string origem) => origem == BaseDadosOrigem.OrdemCompra ? "Ordem de Compra" : "Medição";

    private static string RotuloStatus(string status) => status switch
    {
        MedicaoBmStatus.AguardandoAprovacao => "Aguardando aprovação",
        _ => status,
    };
}

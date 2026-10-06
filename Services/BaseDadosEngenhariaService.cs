using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class BaseDadosEngenhariaService : IBaseDadosEngenhariaService
{
    private static readonly string[] StatusValidos =
        [MedicaoBmStatus.Rascunho, MedicaoBmStatus.AguardandoAprovacao, MedicaoBmStatus.Aprovado, MedicaoBmStatus.Reprovado];

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
            throw new BusinessRuleException("Status do boletim inválido.");
        }

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
                MedicaoBmId = medicao.Id,
                ContratoNumero = medicao.Contrato.Numero,
                NumeroBm = medicao.Numero,
                NumeroReferencia = medicao.NumeroReferencia,
                PeriodoInicio = medicao.PeriodoInicio,
                PeriodoFim = medicao.PeriodoFim,
                Status = medicao.Status,
                FornecedorNome = fornecedor.Nome,
                FornecedorDocumento = !string.IsNullOrWhiteSpace(fornecedor.Cnpj) ? fornecedor.Cnpj : fornecedor.Cpf,
                ValorTotalBm = medicao.ValorTotalMedido,
                ValorLiquidoBm = medicao.ValorTotalMedido + medicao.Acertos.Sum(a => a.PrecoTotal) - medicao.Impostos.Sum(i => i.ValorTotal),
                NfDataEmissao = nota?.DataNf,
                NfNumero = nota?.NumeroNf,
                NfValorTotal = nota?.ValorNota,
            };

            var itensMedidos = medicao.Itens.Where(i => i.QuantidadeMedidaNestaBm > 0).OrderBy(i => i.Id).ToList();
            if (itensMedidos.Count == 0)
            {
                linhas.Add(NovaLinha());
                continue;
            }

            foreach (var item in itensMedidos)
            {
                var rateios = item.RateiosUa.Where(r => r.Quantidade > 0).OrderBy(r => r.UnidadeOrcamentaria.Codigo).ToList();

                BaseDadosEngenhariaLinhaDto LinhaDoItem()
                {
                    var linha = NovaLinha();
                    linha.ItemDescricao = item.DescricaoNoMomento;
                    linha.ItemUnidade = item.UnidadeNoMomento;
                    linha.ValorUnitario = item.ValorUnitarioNoMomento;
                    return linha;
                }

                if (rateios.Count == 0)
                {
                    linhas.Add(LinhaDoItem());
                    continue;
                }

                foreach (var rateio in rateios)
                {
                    var linha = LinhaDoItem();
                    linha.CodigoUa = rateio.UnidadeOrcamentaria.Codigo;
                    linha.QuantidadeUa = rateio.Quantidade;
                    linha.ValorUa = Math.Round(rateio.Quantidade * item.ValorUnitarioNoMomento, 2, MidpointRounding.AwayFromZero);
                    linhas.Add(linha);
                }
            }
        }

        return new BaseDadosEngenhariaDto { Linhas = linhas };
    }

    public byte[] GerarExcel(BaseDadosEngenhariaDto relatorio)
    {
        using var workbook = new XLWorkbook();
        var planilha = workbook.Worksheets.Add("Base de dados");

        string[] cabecalhos =
        [
            "Contrato", "BM", "Referência", "Competência início", "Competência fim", "Status", "Fornecedor", "CNPJ/CPF",
            "Valor total BM", "Valor líquido BM", "Item", "Unidade", "Valor unitário", "Código UA", "Qtde UA",
            "Qtde UA x Valor unit.", "NF - Data emissão", "NF - Número", "NF - Valor total",
        ];
        for (var coluna = 0; coluna < cabecalhos.Length; coluna++)
        {
            planilha.Cell(1, coluna + 1).Value = cabecalhos[coluna];
        }
        planilha.Row(1).Style.Font.Bold = true;

        var linha = 2;
        foreach (var l in relatorio.Linhas)
        {
            planilha.Cell(linha, 1).Value = l.ContratoNumero;
            planilha.Cell(linha, 2).Value = l.NumeroBm;
            planilha.Cell(linha, 3).Value = l.NumeroReferencia ?? string.Empty;
            EscreverData(planilha.Cell(linha, 4), l.PeriodoInicio);
            EscreverData(planilha.Cell(linha, 5), l.PeriodoFim);
            planilha.Cell(linha, 6).Value = RotuloStatus(l.Status);
            planilha.Cell(linha, 7).Value = l.FornecedorNome;
            planilha.Cell(linha, 8).Value = l.FornecedorDocumento ?? string.Empty;
            EscreverValor(planilha.Cell(linha, 9), l.ValorTotalBm);
            EscreverValor(planilha.Cell(linha, 10), l.ValorLiquidoBm);
            planilha.Cell(linha, 11).Value = l.ItemDescricao ?? string.Empty;
            planilha.Cell(linha, 12).Value = l.ItemUnidade ?? string.Empty;
            EscreverValor(planilha.Cell(linha, 13), l.ValorUnitario);
            planilha.Cell(linha, 14).Value = l.CodigoUa ?? string.Empty;
            EscreverQuantidade(planilha.Cell(linha, 15), l.QuantidadeUa);
            EscreverValor(planilha.Cell(linha, 16), l.ValorUa);
            EscreverData(planilha.Cell(linha, 17), l.NfDataEmissao);
            planilha.Cell(linha, 18).Value = l.NfNumero ?? string.Empty;
            EscreverValor(planilha.Cell(linha, 19), l.NfValorTotal);
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

    private static string RotuloStatus(string status) => status switch
    {
        MedicaoBmStatus.AguardandoAprovacao => "Aguardando aprovação",
        _ => status,
    };
}

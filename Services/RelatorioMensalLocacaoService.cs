using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class RelatorioMensalLocacaoService : IRelatorioMensalLocacaoService
{
    private readonly AppDbContext _context;

    public RelatorioMensalLocacaoService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<RelatorioMensalLocacaoDto> GerarAsync(RelatorioMensalLocacaoFiltroDto filtro)
    {
        if (filtro.Mes < 1 || filtro.Mes > 12)
        {
            throw new BusinessRuleException("Mês deve estar entre 1 e 12.");
        }

        if (filtro.Ano < 2000 || filtro.Ano > 2100)
        {
            throw new BusinessRuleException("Ano inválido.");
        }

        var inicioMes = new DateOnly(filtro.Ano, filtro.Mes, 1);
        var diasNoMes = DateTime.DaysInMonth(filtro.Ano, filtro.Mes);
        var fimMes = inicioMes.AddDays(diasNoMes - 1);

        var query = _context.Equipamentos
            .Include(e => e.TipoEquipamento)
            .Where(e => e.Origem == EquipamentoOrigem.Locado && e.ValorMensal != null && e.DataInicioContrato != null)
            .Where(e => e.DataInicioContrato <= fimMes)
            .Where(e => e.DataFimContrato == null || e.DataFimContrato >= inicioMes)
            .AsQueryable();

        if (filtro.TipoEquipamentoId is not null)
        {
            query = query.Where(e => e.TipoEquipamentoId == filtro.TipoEquipamentoId);
        }

        if (!string.IsNullOrWhiteSpace(filtro.FornecedorNome))
        {
            query = query.Where(e => e.FornecedorNome != null && EF.Functions.ILike(e.FornecedorNome, $"%{filtro.FornecedorNome}%"));
        }

        var equipamentos = await query.OrderBy(e => e.TipoEquipamento.Nome).ThenBy(e => e.Id).ToListAsync();

        var itens = equipamentos.Select(e => CalcularItem(e, inicioMes, fimMes, diasNoMes)).ToList();

        return new RelatorioMensalLocacaoDto
        {
            Ano = filtro.Ano,
            Mes = filtro.Mes,
            Itens = itens,
            TotalGeral = itens.Sum(i => i.ValorNoMes),
        };
    }

    public byte[] GerarExcel(RelatorioMensalLocacaoDto relatorio)
    {
        using var workbook = new XLWorkbook();
        var planilha = workbook.Worksheets.Add("Espelho de Medicao");

        string[] cabecalhos = ["Tipo", "Patrimônio", "Nº Série", "Fornecedor", "Valor mensal", "Dias ativos", "Dias no mês", "Valor no mês"];
        for (var coluna = 0; coluna < cabecalhos.Length; coluna++)
        {
            planilha.Cell(1, coluna + 1).Value = cabecalhos[coluna];
        }
        planilha.Row(1).Style.Font.Bold = true;

        var linha = 2;
        foreach (var item in relatorio.Itens)
        {
            planilha.Cell(linha, 1).Value = item.TipoEquipamentoNome;
            planilha.Cell(linha, 2).Value = item.Patrimonio ?? "-";
            planilha.Cell(linha, 3).Value = item.NumeroSerie ?? "-";
            planilha.Cell(linha, 4).Value = item.FornecedorNome ?? "-";
            planilha.Cell(linha, 5).Value = item.ValorMensal;
            planilha.Cell(linha, 6).Value = item.DiasAtivos;
            planilha.Cell(linha, 7).Value = item.DiasNoMes;
            planilha.Cell(linha, 8).Value = item.ValorNoMes;
            linha++;
        }

        planilha.Cell(linha, 7).Value = "Total";
        planilha.Cell(linha, 7).Style.Font.Bold = true;
        planilha.Cell(linha, 8).Value = relatorio.TotalGeral;
        planilha.Cell(linha, 8).Style.Font.Bold = true;

        planilha.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static RelatorioMensalLocacaoItemDto CalcularItem(Equipamento equipamento, DateOnly inicioMes, DateOnly fimMes, int diasNoMes)
    {
        var inicioAtivo = equipamento.DataInicioContrato!.Value > inicioMes ? equipamento.DataInicioContrato.Value : inicioMes;
        var fimAtivo = equipamento.DataFimContrato is not null && equipamento.DataFimContrato.Value < fimMes
            ? equipamento.DataFimContrato.Value
            : fimMes;

        var diasAtivos = Math.Clamp(fimAtivo.DayNumber - inicioAtivo.DayNumber + 1, 0, diasNoMes);

        var valorMensal = equipamento.ValorMensal!.Value;
        var valorNoMes = diasAtivos == diasNoMes
            ? valorMensal
            : Math.Round(valorMensal * diasAtivos / diasNoMes, 2, MidpointRounding.AwayFromZero);

        return new RelatorioMensalLocacaoItemDto
        {
            EquipamentoId = equipamento.Id,
            TipoEquipamentoNome = equipamento.TipoEquipamento.Nome,
            Patrimonio = equipamento.Patrimonio,
            NumeroSerie = equipamento.NumeroSerie,
            FornecedorNome = equipamento.FornecedorNome,
            ValorMensal = valorMensal,
            DiasAtivos = diasAtivos,
            DiasNoMes = diasNoMes,
            ValorNoMes = valorNoMes,
        };
    }
}

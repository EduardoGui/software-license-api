using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class RelatorioMensalCustoLicencasService : IRelatorioMensalCustoLicencasService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;

    public RelatorioMensalCustoLicencasService(AppDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<RelatorioMensalCustoLicencasDto> GerarAsync(RelatorioMensalCustoLicencasFiltroDto filtro)
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
        var hoje = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

        var query = _context.Licencas
            .Where(l => l.DataInicio <= fimMes && l.DataTerminoPrevisto >= inicioMes)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Nome))
        {
            query = query.Where(l => EF.Functions.ILike(l.Nome, $"%{filtro.Nome}%"));
        }

        var licencas = await query.OrderBy(l => l.Nome).ToListAsync();
        var licencaIds = licencas.Select(l => l.Id).ToList();

        var valores = await _context.LicencaValores
            .Where(v => licencaIds.Contains(v.LicencaId))
            .OrderBy(v => v.DataVigenciaInicio)
            .ThenBy(v => v.Id)
            .ToListAsync();
        var valoresPorLicenca = valores
            .GroupBy(v => v.LicencaId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var itens = licencas
            .Select(l => CalcularItem(l, valoresPorLicenca.GetValueOrDefault(l.Id, []), inicioMes, fimMes, diasNoMes, hoje))
            .ToList();

        return new RelatorioMensalCustoLicencasDto
        {
            Ano = filtro.Ano,
            Mes = filtro.Mes,
            Itens = itens,
            TotalGeral = itens.Sum(i => i.ValorNoMes),
        };
    }

    public byte[] GerarExcel(RelatorioMensalCustoLicencasDto relatorio)
    {
        using var workbook = new XLWorkbook();
        var planilha = workbook.Worksheets.Add("Custo Mensal de Licencas");

        string[] cabecalhos = ["Licença", "Periodicidade", "Valor vigente", "Dias ativos", "Dias no mês", "Valor no mês"];
        for (var coluna = 0; coluna < cabecalhos.Length; coluna++)
        {
            planilha.Cell(1, coluna + 1).Value = cabecalhos[coluna];
        }
        planilha.Row(1).Style.Font.Bold = true;

        var linha = 2;
        foreach (var item in relatorio.Itens)
        {
            planilha.Cell(linha, 1).Value = item.Nome;
            planilha.Cell(linha, 2).Value = item.Periodicidade ?? "-";
            if (item.ValorVigente is not null)
            {
                planilha.Cell(linha, 3).Value = item.ValorVigente.Value;
            }
            else
            {
                planilha.Cell(linha, 3).Value = "-";
            }
            planilha.Cell(linha, 4).Value = item.DiasAtivos;
            planilha.Cell(linha, 5).Value = item.DiasNoMes;
            planilha.Cell(linha, 6).Value = item.ValorNoMes;
            linha++;
        }

        planilha.Cell(linha, 5).Value = "Total";
        planilha.Cell(linha, 5).Style.Font.Bold = true;
        planilha.Cell(linha, 6).Value = relatorio.TotalGeral;
        planilha.Cell(linha, 6).Style.Font.Bold = true;

        planilha.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static RelatorioMensalCustoLicencasItemDto CalcularItem(
        Licenca licenca, List<LicencaValor> valores, DateOnly inicioMes, DateOnly fimMes, int diasNoMes, DateOnly hoje)
    {
        var inicioAtivo = licenca.DataInicio > inicioMes ? licenca.DataInicio : inicioMes;
        var fimAtivo = licenca.DataTerminoPrevisto < fimMes ? licenca.DataTerminoPrevisto : fimMes;
        var diasAtivos = Math.Clamp(fimAtivo.DayNumber - inicioAtivo.DayNumber + 1, 0, diasNoMes);

        var valorNoMes = 0m;
        for (var i = 0; i < valores.Count; i++)
        {
            var vigencia = valores[i];
            var fimVigencia = i + 1 < valores.Count ? valores[i + 1].DataVigenciaInicio.AddDays(-1) : fimAtivo;

            var inicioSegmento = vigencia.DataVigenciaInicio > inicioAtivo ? vigencia.DataVigenciaInicio : inicioAtivo;
            var fimSegmento = fimVigencia < fimAtivo ? fimVigencia : fimAtivo;

            if (inicioSegmento > fimSegmento)
            {
                continue;
            }

            var diasSegmento = fimSegmento.DayNumber - inicioSegmento.DayNumber + 1;
            var valorMensalEquivalente = vigencia.Periodicidade == LicencaPeriodicidade.Anual ? vigencia.Valor / 12 : vigencia.Valor;
            var valorSegmento = diasSegmento == diasNoMes
                ? valorMensalEquivalente
                : Math.Round(valorMensalEquivalente * diasSegmento / diasNoMes, 2, MidpointRounding.AwayFromZero);

            valorNoMes += valorSegmento;
        }

        var valorVigente = valores.LastOrDefault(v => v.DataVigenciaInicio <= hoje);

        return new RelatorioMensalCustoLicencasItemDto
        {
            LicencaId = licenca.Id,
            Nome = licenca.Nome,
            Periodicidade = valorVigente?.Periodicidade,
            ValorVigente = valorVigente?.Valor,
            DiasAtivos = diasAtivos,
            DiasNoMes = diasNoMes,
            ValorNoMes = valorNoMes,
        };
    }
}

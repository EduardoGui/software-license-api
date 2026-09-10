using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class RelatorioMensalCustoLicencasService : IRelatorioMensalCustoLicencasService
{
    private const string SemTipoDefinido = "Sem tipo definido";
    private const string SemUsuarioAlocado = "(sem usuário alocado)";

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

        var alocacoes = await _context.UsuarioLicencas
            .Include(m => m.Usuario)
            .Where(m => licencaIds.Contains(m.LicencaId) && m.DataInicio <= fimMes && (m.DataFim == null || m.DataFim >= inicioMes))
            .ToListAsync();
        var alocacoesPorLicenca = alocacoes
            .GroupBy(m => m.LicencaId)
            .ToDictionary(g => g.Key, g => g.OrderBy(m => m.Usuario.Nome).ToList());

        var itensLicenca = licencas
            .Select(l => CalcularLicenca(
                l, valoresPorLicenca.GetValueOrDefault(l.Id, []), alocacoesPorLicenca.GetValueOrDefault(l.Id, []), inicioMes, fimMes, diasNoMes))
            .ToList();

        var gruposMensal = MontarGrupos(itensLicenca.Where(i => i.Periodicidade != LicencaPeriodicidade.Anual));
        var gruposAnual = MontarGrupos(itensLicenca.Where(i => i.Periodicidade == LicencaPeriodicidade.Anual));
        var subtotalMensal = gruposMensal.Sum(g => g.Subtotal);
        var subtotalAnual = gruposAnual.Sum(g => g.Subtotal);

        return new RelatorioMensalCustoLicencasDto
        {
            Ano = filtro.Ano,
            Mes = filtro.Mes,
            GruposMensal = gruposMensal,
            SubtotalMensal = subtotalMensal,
            GruposAnual = gruposAnual,
            SubtotalAnual = subtotalAnual,
            ValorTotal = subtotalMensal + subtotalAnual,
        };
    }

    private static List<RelatorioMensalCustoLicencasGrupoDto> MontarGrupos(
        IEnumerable<(string? Tipo, string Periodicidade, RelatorioMensalCustoLicencasItemDto Item)> itens) =>
        itens
            .GroupBy(i => string.IsNullOrWhiteSpace(i.Tipo) ? SemTipoDefinido : i.Tipo)
            .OrderBy(g => g.Key == SemTipoDefinido ? 1 : 0)
            .ThenBy(g => g.Key)
            .Select(g => new RelatorioMensalCustoLicencasGrupoDto
            {
                Tipo = g.Key,
                Licencas = g.Select(i => i.Item).OrderBy(i => i.Nome).ToList(),
                Subtotal = g.Sum(i => i.Item.Subtotal),
            })
            .ToList();

    public byte[] GerarExcel(RelatorioMensalCustoLicencasDto relatorio)
    {
        using var workbook = new XLWorkbook();
        var planilha = workbook.Worksheets.Add("Custo Mensal de Licencas");

        string[] cabecalhos = ["Tipo", "Licença", "Usuário", "Dias ativos", "Valor proporcional"];
        for (var coluna = 0; coluna < cabecalhos.Length; coluna++)
        {
            planilha.Cell(1, coluna + 1).Value = cabecalhos[coluna];
        }
        planilha.Row(1).Style.Font.Bold = true;

        var linha = 2;
        linha = EscreverSecao(planilha, "CUSTO MENSAL", relatorio.GruposMensal, relatorio.SubtotalMensal, linha);
        linha = EscreverSecao(planilha, "CUSTO ANUAL (equivalente mensal)", relatorio.GruposAnual, relatorio.SubtotalAnual, linha);

        planilha.Cell(linha, 2).Value = "Medição da Empresa (Total)";
        planilha.Cell(linha, 2).Style.Font.Bold = true;
        planilha.Cell(linha, 5).Value = relatorio.ValorTotal;
        planilha.Cell(linha, 5).Style.Font.Bold = true;

        planilha.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static int EscreverSecao(
        IXLWorksheet planilha, string titulo, List<RelatorioMensalCustoLicencasGrupoDto> grupos, decimal subtotalSecao, int linha)
    {
        if (grupos.Count == 0)
        {
            return linha;
        }

        planilha.Cell(linha, 1).Value = titulo;
        planilha.Cell(linha, 1).Style.Font.Bold = true;
        linha++;

        foreach (var grupo in grupos)
        {
            foreach (var licenca in grupo.Licencas)
            {
                foreach (var usuario in licenca.Usuarios)
                {
                    planilha.Cell(linha, 1).Value = grupo.Tipo;
                    planilha.Cell(linha, 2).Value = licenca.Nome;
                    planilha.Cell(linha, 3).Value = usuario.UsuarioNome;
                    planilha.Cell(linha, 4).Value = usuario.DiasAtivos;
                    planilha.Cell(linha, 5).Value = usuario.ValorProporcional;
                    linha++;
                }

                planilha.Cell(linha, 3).Value = $"Subtotal — {licenca.Nome}";
                planilha.Cell(linha, 3).Style.Font.Italic = true;
                planilha.Cell(linha, 5).Value = licenca.Subtotal;
                planilha.Cell(linha, 5).Style.Font.Italic = true;
                linha++;
            }

            planilha.Cell(linha, 2).Value = $"Subtotal — {grupo.Tipo}";
            planilha.Cell(linha, 2).Style.Font.Bold = true;
            planilha.Cell(linha, 5).Value = grupo.Subtotal;
            planilha.Cell(linha, 5).Style.Font.Bold = true;
            linha++;
            linha++;
        }

        planilha.Cell(linha, 2).Value = $"Subtotal — {titulo}";
        planilha.Cell(linha, 2).Style.Font.Bold = true;
        planilha.Cell(linha, 5).Value = subtotalSecao;
        planilha.Cell(linha, 5).Style.Font.Bold = true;
        linha += 2;

        return linha;
    }

    private static (string? Tipo, string Periodicidade, RelatorioMensalCustoLicencasItemDto Item) CalcularLicenca(
        Licenca licenca, List<LicencaValor> valores, List<UsuarioLicenca> alocacoes, DateOnly inicioMes, DateOnly fimMes, int diasNoMes)
    {
        var inicioAtivo = licenca.DataInicio > inicioMes ? licenca.DataInicio : inicioMes;
        var fimAtivo = licenca.DataTerminoPrevisto < fimMes ? licenca.DataTerminoPrevisto : fimMes;
        var diasAtivosLicenca = Math.Clamp(fimAtivo.DayNumber - inicioAtivo.DayNumber + 1, 0, diasNoMes);

        // Valor mensal equivalente cadastrado (já convertendo Anual -> Valor/12), prorateado pelos
        // dias em que a licença ficou ativa no mês. O SIGNIFICADO desse valor depende da forma de
        // cobrança (ver abaixo): por vaga (preço de uma unidade) ou pacote (preço fechado do lote
        // inteiro de QuantidadeTotal vagas).
        var valorMensalCadastrado = 0m;
        var periodicidadeAtual = LicencaPeriodicidade.Mensal;
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

            valorMensalCadastrado += valorSegmento;
            periodicidadeAtual = vigencia.Periodicidade;
        }

        // PorVaga: o valor cadastrado é por unidade - custo total = valor x QuantidadeTotal (a
        // empresa paga por todas as vagas, alocadas ou não), e cada usuário aloca o valor cheio.
        // Pacote: o valor cadastrado já é o preço fechado do lote inteiro - custo total = o
        // próprio valor (sem multiplicar), e cada usuário aloca uma fração igual dele.
        var ehPacote = licenca.FormaCobranca == LicencaFormaCobranca.Pacote;
        var subtotalLicenca = ehPacote
            ? Math.Round(valorMensalCadastrado, 2, MidpointRounding.AwayFromZero)
            : Math.Round(valorMensalCadastrado * licenca.QuantidadeTotal, 2, MidpointRounding.AwayFromZero);
        var valorBasePorUsuario = ehPacote
            ? (licenca.QuantidadeTotal > 0 ? valorMensalCadastrado / licenca.QuantidadeTotal : 0m)
            : valorMensalCadastrado;

        var usuarios = new List<RelatorioMensalCustoLicencasUsuarioDto>();
        foreach (var alocacao in alocacoes)
        {
            var inicioAtivoUsuario = alocacao.DataInicio > inicioAtivo ? alocacao.DataInicio : inicioAtivo;
            var fimAlocacao = alocacao.DataFim ?? fimAtivo;
            var fimAtivoUsuario = fimAlocacao < fimAtivo ? fimAlocacao : fimAtivo;
            var diasAtivosUsuario = Math.Clamp(fimAtivoUsuario.DayNumber - inicioAtivoUsuario.DayNumber + 1, 0, diasNoMes);

            if (diasAtivosUsuario <= 0)
            {
                continue;
            }

            usuarios.Add(new RelatorioMensalCustoLicencasUsuarioDto
            {
                UsuarioId = alocacao.UsuarioId,
                UsuarioNome = alocacao.Usuario.Nome,
                DiasAtivos = diasAtivosUsuario,
                ValorProporcional = Math.Round(valorBasePorUsuario * diasAtivosUsuario / diasNoMes, 2, MidpointRounding.AwayFromZero),
            });
        }

        if (usuarios.Count == 0)
        {
            usuarios.Add(new RelatorioMensalCustoLicencasUsuarioDto
            {
                UsuarioId = null,
                UsuarioNome = SemUsuarioAlocado,
                DiasAtivos = diasAtivosLicenca,
                ValorProporcional = subtotalLicenca,
            });
        }

        var item = new RelatorioMensalCustoLicencasItemDto
        {
            LicencaId = licenca.Id,
            Nome = licenca.Nome,
            DiasNoMes = diasNoMes,
            Usuarios = usuarios,
            Subtotal = subtotalLicenca,
        };

        return (licenca.Tipo, periodicidadeAtual, item);
    }
}

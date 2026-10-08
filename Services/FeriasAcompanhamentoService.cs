using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

// Visão "uma linha por colaborador" para acompanhar férias: quanto tem, quanto já tirou/marcou, o que
// falta marcar, o prazo e uma situação principal. Só lê dados que já existem (nada novo no banco).
public class FeriasAcompanhamentoService : IFeriasAcompanhamentoService
{
    // Meta da empresa (decisão do usuário, 2026-10-08): ninguém passa mais de 6 meses sem tirar nem
    // marcar férias. Fixo no código por enquanto; vira campo da política se precisarem variar.
    public const int MesesMeta = 6;
    private const int DiasProximasFerias = 60;
    private const int DiasAntecedenciaPadrao = 30;

    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly IPeriodoFeriasService _periodoFeriasService;

    public FeriasAcompanhamentoService(AppDbContext context, TimeProvider timeProvider, IPeriodoFeriasService periodoFeriasService)
    {
        _context = context;
        _timeProvider = timeProvider;
        _periodoFeriasService = periodoFeriasService;
    }

    public async Task<FeriasAcompanhamentoDto> ObterAsync(FeriasAcompanhamentoFiltroDto filtro)
    {
        if (!string.IsNullOrWhiteSpace(filtro.Situacao) && !FeriasAcompanhamentoFiltroSituacao.Todos.Contains(filtro.Situacao))
        {
            throw new BusinessRuleException("Situação inválida.");
        }

        var hoje = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

        var usuarios = (await _context.Usuarios
                .Include(u => u.Setor)
                .Where(u => UsuarioTipo.ComModuloDeFerias.Contains(u.Tipo!))
                .ToListAsync())
            .Where(u => UsuarioStatus.Calcular(u, hoje) == UsuarioStatus.Ativo)
            .ToList();

        var periodoAtualPorUsuario = (await _periodoFeriasService.GetAllAsync(new PeriodoFeriasFiltroDto()))
            .GroupBy(p => p.UsuarioId)
            .ToDictionary(g => g.Key, g => g.First());

        var antecedenciaPorTipo = (await _context.PoliticasFerias.Where(p => p.Ativa && p.TipoVinculo != null).ToListAsync())
            .ToDictionary(p => p.TipoVinculo!, p => p.DiasAntecedenciaMarcacaoCompulsoria);

        var programacoesPorUsuario = (await _context.ProgramacoesFerias
                .Include(p => p.PeriodoFerias)
                .Where(p => p.Status == ProgramacaoFeriasStatus.Aprovada || p.Status == ProgramacaoFeriasStatus.Solicitada)
                .ToListAsync())
            .ToLookup(p => p.PeriodoFerias.UsuarioId);

        var recessosPorUsuario = (await _context.RecessosColaborador
                .Include(r => r.RecessoCorporativo)
                .ToListAsync())
            .ToLookup(r => r.UsuarioId);

        var todas = usuarios
            .Select(u => MontarLinha(
                u, hoje, periodoAtualPorUsuario.GetValueOrDefault(u.Id), antecedenciaPorTipo.GetValueOrDefault(u.Tipo!, DiasAntecedenciaPadrao),
                programacoesPorUsuario[u.Id].ToList(), recessosPorUsuario[u.Id].ToList()))
            .ToList();

        var resumo = new FeriasAcompanhamentoResumoDto
        {
            Total = todas.Count,
            EmFeriasAgora = todas.Count(l => l.EmFeriasAgora),
            SaemEm60Dias = todas.Count(l => l.SaemEm60Dias),
            SemProgramar = todas.Count(l => l.SemProgramar),
            Prazo = todas.Count(l => l.Prazo),
            SaldoNegativo = todas.Count(l => l.SaldoNegativo),
            AguardandoAprovacao = todas.Count(l => l.AguardandoAprovacao),
            SemPeriodo = todas.Count(l => l.PeriodoFeriasId is null),
        };

        var linhas = todas.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(filtro.Nome))
        {
            var nome = filtro.Nome.Trim();
            linhas = linhas.Where(l => l.UsuarioNome.Contains(nome, StringComparison.OrdinalIgnoreCase));
        }
        if (filtro.SetorId is not null)
        {
            var setorId = filtro.SetorId.Value;
            var usuariosDoSetor = usuarios.Where(u => u.SetorId == setorId).Select(u => u.Id).ToHashSet();
            linhas = linhas.Where(l => usuariosDoSetor.Contains(l.UsuarioId));
        }
        if (!string.IsNullOrWhiteSpace(filtro.Situacao))
        {
            linhas = linhas.Where(AplicaFiltro(filtro.Situacao));
        }

        return new FeriasAcompanhamentoDto
        {
            MesesMeta = MesesMeta,
            Resumo = resumo,
            Linhas = linhas
                .OrderBy(l => Array.IndexOf(FeriasAcompanhamentoSituacao.Ordem, l.Situacao))
                .ThenBy(l => l.DiasParaVencer ?? int.MaxValue)
                .ThenBy(l => l.UsuarioNome, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };
    }

    public byte[] GerarExcel(FeriasAcompanhamentoDto acompanhamento)
    {
        using var workbook = new XLWorkbook();
        var planilha = workbook.Worksheets.Add("Acompanhamento de Férias");

        string[] cabecalhos =
            ["Colaborador", "Setor", "Vínculo", "Direito", "Tirou", "Marcado", "Recesso", "A marcar", "Prazo para tirar", "Férias (início)", "Férias (fim)", "Situação", "Detalhe"];
        for (var coluna = 0; coluna < cabecalhos.Length; coluna++)
        {
            planilha.Cell(1, coluna + 1).Value = cabecalhos[coluna];
        }
        planilha.Row(1).Style.Font.Bold = true;

        var linha = 2;
        foreach (var item in acompanhamento.Linhas)
        {
            planilha.Cell(linha, 1).Value = item.UsuarioNome;
            planilha.Cell(linha, 2).Value = item.SetorNome ?? "-";
            planilha.Cell(linha, 3).Value = item.Tipo;
            if (item.PeriodoFeriasId is not null)
            {
                planilha.Cell(linha, 4).Value = item.Direito;
                planilha.Cell(linha, 5).Value = item.Tirou;
                planilha.Cell(linha, 6).Value = item.Marcado;
                planilha.Cell(linha, 7).Value = item.Recesso;
                planilha.Cell(linha, 8).Value = item.ASaldo;
            }
            planilha.Cell(linha, 9).Value = item.FimConcessivo is null ? "-" : item.FimConcessivo.Value.ToString("dd/MM/yyyy");
            planilha.Cell(linha, 10).Value = item.FeriasInicio is null ? "-" : item.FeriasInicio.Value.ToString("dd/MM/yyyy");
            planilha.Cell(linha, 11).Value = item.FeriasFim is null ? "-" : item.FeriasFim.Value.ToString("dd/MM/yyyy");
            planilha.Cell(linha, 12).Value = FeriasAcompanhamentoSituacao.Rotulo(item.Situacao);
            planilha.Cell(linha, 13).Value = item.SituacaoDetalhe;
            linha++;
        }

        planilha.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static Func<FeriasAcompanhamentoLinhaDto, bool> AplicaFiltro(string situacao) => situacao switch
    {
        FeriasAcompanhamentoFiltroSituacao.EmFeriasAgora => l => l.EmFeriasAgora,
        FeriasAcompanhamentoFiltroSituacao.SaemEm60Dias => l => l.SaemEm60Dias,
        FeriasAcompanhamentoFiltroSituacao.SemProgramar => l => l.SemProgramar,
        FeriasAcompanhamentoFiltroSituacao.Prazo => l => l.Prazo,
        FeriasAcompanhamentoFiltroSituacao.SaldoNegativo => l => l.SaldoNegativo,
        FeriasAcompanhamentoFiltroSituacao.AguardandoAprovacao => l => l.AguardandoAprovacao,
        _ => l => l.PeriodoFeriasId is null,
    };

    private static FeriasAcompanhamentoLinhaDto MontarLinha(
        Usuario usuario, DateOnly hoje, PeriodoFeriasDto? periodo, int diasAntecedencia,
        List<ProgramacaoFerias> programacoes, List<RecessoColaborador> recessos)
    {
        var linha = new FeriasAcompanhamentoLinhaDto
        {
            UsuarioId = usuario.Id,
            UsuarioNome = usuario.Nome,
            SetorNome = usuario.Setor?.Nome,
            Tipo = usuario.Tipo ?? string.Empty,
        };

        var aprovadas = programacoes.Where(p => p.Status == ProgramacaoFeriasStatus.Aprovada).OrderBy(p => p.DataInicio).ToList();
        var solicitadas = programacoes
            .Where(p => p.Status == ProgramacaoFeriasStatus.Solicitada && p.DataFim >= hoje)
            .OrderBy(p => p.DataInicio)
            .ToList();

        var emCurso = aprovadas.FirstOrDefault(p => p.DataInicio <= hoje && p.DataFim >= hoje);
        var proxima = aprovadas.FirstOrDefault(p => p.DataInicio > hoje);
        var destaque = emCurso ?? proxima ?? solicitadas.FirstOrDefault();

        linha.EmFeriasAgora = emCurso is not null;
        linha.AguardandoAprovacao = solicitadas.Count > 0;
        linha.SaemEm60Dias = emCurso is null && proxima is not null && proxima.DataInicio <= hoje.AddDays(DiasProximasFerias);
        linha.FeriasInicio = destaque?.DataInicio;
        linha.FeriasFim = destaque?.DataFim;

        // Há quanto tempo a pessoa não tira férias: fim do último gozo; se nunca tirou, desde o início do vínculo.
        var ultimoGozo = aprovadas.Where(p => p.DataFim <= hoje).Select(p => (DateOnly?)p.DataFim).Max();
        var referencia = emCurso is not null ? hoje : ultimoGozo ?? usuario.DataInicio;
        linha.SemFeriasDesde = referencia;
        linha.MesesSemFerias = MesesCompletos(referencia, hoje);
        var temPlanejamento = emCurso is not null || proxima is not null || solicitadas.Count > 0;

        if (periodo is null)
        {
            linha.Situacao = FeriasAcompanhamentoSituacao.SemPeriodo;
            linha.SituacaoDetalhe = "Período de férias ainda não gerado.";
            return linha;
        }

        // Recesso separado do resto: Consumido/Comprometido do período somam recesso + férias + abono.
        var recessosDoPeriodo = recessos.Where(r => r.PeriodoFeriasId == periodo.Id).ToList();
        var recessoPassado = recessosDoPeriodo.Where(r => r.RecessoCorporativo.DataInicio <= hoje).Sum(r => r.DiasAbatidos);
        var recessoFuturo = recessosDoPeriodo.Where(r => r.RecessoCorporativo.DataInicio > hoje).Sum(r => r.DiasAbatidos);

        linha.PeriodoFeriasId = periodo.Id;
        linha.InicioAquisitivo = periodo.InicioAquisitivo;
        linha.FimConcessivo = periodo.FimConcessivo;
        linha.DiasParaVencer = periodo.FimConcessivo.DayNumber - hoje.DayNumber;
        linha.Direito = periodo.DiasDireito;
        linha.Recesso = recessoPassado + recessoFuturo;
        linha.Tirou = Math.Max(0, periodo.Consumido - recessoPassado);
        linha.Marcado = Math.Max(0, periodo.Comprometido - recessoFuturo);
        linha.ASaldo = periodo.SaldoDisponivel;

        linha.SaldoNegativo = linha.ASaldo < 0;
        linha.Prazo = linha.ASaldo > 0 && linha.DiasParaVencer <= diasAntecedencia;
        linha.SemProgramar = linha.ASaldo > 0 && !temPlanejamento && linha.MesesSemFerias >= MesesMeta;

        (linha.Situacao, linha.SituacaoDetalhe) = DefinirSituacao(linha, emCurso, proxima, solicitadas.FirstOrDefault());
        return linha;
    }

    private static (string Situacao, string Detalhe) DefinirSituacao(
        FeriasAcompanhamentoLinhaDto l, ProgramacaoFerias? emCurso, ProgramacaoFerias? proxima, ProgramacaoFerias? solicitada)
    {
        if (l.SaldoNegativo)
        {
            return (FeriasAcompanhamentoSituacao.SaldoNegativo, $"Usou {-l.ASaldo} dia(s) além do direito do período.");
        }
        if (l.Prazo && l.DiasParaVencer < 0)
        {
            return (FeriasAcompanhamentoSituacao.Vencido, $"O prazo para tirar venceu em {Data(l.FimConcessivo)} com {l.ASaldo} dia(s) sem usar.");
        }
        if (l.Prazo)
        {
            return (FeriasAcompanhamentoSituacao.VenceEmBreve, $"O prazo para tirar vence em {l.DiasParaVencer} dia(s) ({Data(l.FimConcessivo)}); faltam {l.ASaldo} dia(s).");
        }
        if (l.SemProgramar)
        {
            return (FeriasAcompanhamentoSituacao.SemProgramar,
                $"Sem férias desde {Data(l.SemFeriasDesde)} ({l.MesesSemFerias} meses) e nada marcado; a meta é no máximo {MesesMeta} meses.");
        }
        if (solicitada is not null && emCurso is null && proxima is null)
        {
            return (FeriasAcompanhamentoSituacao.AguardandoAprovacao, $"Pedido de {Data(solicitada.DataInicio)} a {Data(solicitada.DataFim)} aguardando aprovação.");
        }
        if (emCurso is not null)
        {
            return (FeriasAcompanhamentoSituacao.EmFerias, $"De férias até {Data(emCurso.DataFim)}.");
        }
        if (proxima is not null)
        {
            return (FeriasAcompanhamentoSituacao.ProximasFerias, $"Férias de {Data(proxima.DataInicio)} a {Data(proxima.DataFim)}.");
        }
        if (solicitada is not null)
        {
            return (FeriasAcompanhamentoSituacao.AguardandoAprovacao, $"Pedido de {Data(solicitada.DataInicio)} a {Data(solicitada.DataFim)} aguardando aprovação.");
        }
        if (l.ASaldo > 0)
        {
            return (FeriasAcompanhamentoSituacao.SemNadaMarcado, $"{l.ASaldo} dia(s) para marcar; sem programação.");
        }

        return (FeriasAcompanhamentoSituacao.EmDia, "Saldo zerado.");
    }

    private static string Data(DateOnly? data) => data is null ? "-" : data.Value.ToString("dd/MM/yyyy");

    // Meses completos entre duas datas (5 meses e 29 dias = 5).
    private static int MesesCompletos(DateOnly de, DateOnly ate)
    {
        var meses = (ate.Year - de.Year) * 12 + ate.Month - de.Month;
        if (ate.Day < de.Day)
        {
            meses--;
        }

        return Math.Max(0, meses);
    }
}

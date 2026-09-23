using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;

namespace SoftwareLicense.Api.Services;

public class FeriasConsolidadoService : IFeriasConsolidadoService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly IPeriodoFeriasService _periodoFeriasService;
    private readonly IProgramacaoFeriasService _programacaoFeriasService;

    public FeriasConsolidadoService(
        AppDbContext context, TimeProvider timeProvider,
        IPeriodoFeriasService periodoFeriasService, IProgramacaoFeriasService programacaoFeriasService)
    {
        _context = context;
        _timeProvider = timeProvider;
        _periodoFeriasService = periodoFeriasService;
        _programacaoFeriasService = programacaoFeriasService;
    }

    public async Task<FeriasDashboardDto> ObterDashboardAsync()
    {
        var hoje = Hoje();

        var usuariosComFeriasAtivos = (await _context.Usuarios.Where(u => UsuarioTipo.ComModuloDeFerias.Contains(u.Tipo!)).ToListAsync())
            .Where(u => UsuarioStatus.Calcular(u, hoje) == UsuarioStatus.Ativo)
            .ToList();

        var periodoAtualPorUsuario = (await _periodoFeriasService.GetAllAsync(new PeriodoFeriasFiltroDto()))
            .GroupBy(p => p.UsuarioId)
            .ToDictionary(g => g.Key, g => g.First());

        var politicasPorTipo = (await _context.PoliticasFerias.Where(p => p.Ativa).ToListAsync())
            .Where(p => p.TipoVinculo is not null)
            .ToDictionary(p => p.TipoVinculo!, p => p);

        var concessivosVencendo = new List<ConcessivoVencendoDto>();
        foreach (var usuario in usuariosComFeriasAtivos)
        {
            var politica = politicasPorTipo.GetValueOrDefault(usuario.Tipo!);
            if (!periodoAtualPorUsuario.TryGetValue(usuario.Id, out var periodo) || periodo.SaldoDisponivel <= 0 || politica is null)
            {
                continue;
            }

            var diasParaVencer = periodo.FimConcessivo.DayNumber - hoje.DayNumber;
            if (diasParaVencer > politica.DiasAntecedenciaMarcacaoCompulsoria)
            {
                continue;
            }

            concessivosVencendo.Add(new ConcessivoVencendoDto
            {
                UsuarioId = usuario.Id,
                UsuarioNome = usuario.Nome,
                PeriodoFeriasId = periodo.Id,
                FimConcessivo = periodo.FimConcessivo,
                SaldoDisponivel = periodo.SaldoDisponivel,
                DiasParaVencer = diasParaVencer,
            });
        }

        var filaAprovacao = await _programacaoFeriasService.GetPendentesAprovacaoAsync();

        var programacoesEmGozoHoje = await _context.ProgramacoesFerias
            .CountAsync(p => p.Status == ProgramacaoFeriasStatus.Aprovada && p.DataInicio <= hoje && p.DataFim >= hoje);

        var recessosConfirmadosAnoAtual = await _context.RecessosCorporativos
            .CountAsync(r => r.Status == RecessoCorporativoStatus.Confirmado && r.DataInicio.Year == hoje.Year);

        return new FeriasDashboardDto
        {
            ColaboradoresPj = usuariosComFeriasAtivos.Count,
            ColaboradoresSemPeriodoGerado = usuariosComFeriasAtivos.Count(u => !periodoAtualPorUsuario.ContainsKey(u.Id)),
            SaldoTotalDisponivel = usuariosComFeriasAtivos.Sum(u => periodoAtualPorUsuario.GetValueOrDefault(u.Id)?.SaldoDisponivel ?? 0),
            ProgramacoesPendentesAprovacao = filaAprovacao.Count,
            ProgramacoesEmGozoHoje = programacoesEmGozoHoje,
            RecessosConfirmadosAnoAtual = recessosConfirmadosAnoAtual,
            ConcessivosProximosDoVencimento = concessivosVencendo.OrderBy(c => c.DiasParaVencer).Take(10).ToList(),
            FilaAprovacao = filaAprovacao.Take(10).ToList(),
        };
    }

    public async Task<List<FeriasCalendarioUsuarioDto>> ObterCalendarioAsync(FeriasCalendarioFiltroDto filtro)
    {
        var hoje = Hoje();
        var de = filtro.De ?? new DateOnly(hoje.Year, hoje.Month, 1);
        var ate = filtro.Ate ?? de.AddMonths(1).AddDays(-1);

        var usuariosQuery = _context.Usuarios.Where(u => UsuarioTipo.ComModuloDeFerias.Contains(u.Tipo!)).AsQueryable();
        if (filtro.SetorId is not null)
        {
            usuariosQuery = usuariosQuery.Where(u => u.SetorId == filtro.SetorId);
        }
        if (filtro.UsuarioId is not null)
        {
            usuariosQuery = usuariosQuery.Where(u => u.Id == filtro.UsuarioId);
        }

        var usuarios = await usuariosQuery.OrderBy(u => u.Nome).ToListAsync();
        var usuarioIds = usuarios.Select(u => u.Id).ToHashSet();

        var programacoes = await _context.ProgramacoesFerias
            .Include(p => p.PeriodoFerias)
            .Where(p => p.Status == ProgramacaoFeriasStatus.Aprovada && p.DataInicio <= ate && p.DataFim >= de)
            .ToListAsync();

        var recessos = await _context.RecessosColaborador
            .Include(c => c.RecessoCorporativo)
            .Where(c => c.RecessoCorporativo.Status == RecessoCorporativoStatus.Confirmado
                && c.RecessoCorporativo.DataInicio <= ate && c.RecessoCorporativo.DataFim >= de)
            .ToListAsync();

        var eventosPorUsuario = new Dictionary<int, List<FeriasCalendarioEventoDto>>();

        foreach (var p in programacoes.Where(p => usuarioIds.Contains(p.PeriodoFerias.UsuarioId)))
        {
            var lista = eventosPorUsuario.GetValueOrDefault(p.PeriodoFerias.UsuarioId, []);
            lista.Add(new FeriasCalendarioEventoDto
            {
                Tipo = FeriasCalendarioEventoTipo.Programacao,
                DataInicio = p.DataInicio,
                DataFim = p.DataFim,
                Descricao = $"Férias ({p.QuantidadeDias} dia(s))",
            });
            eventosPorUsuario[p.PeriodoFerias.UsuarioId] = lista;
        }

        foreach (var c in recessos.Where(c => usuarioIds.Contains(c.UsuarioId)))
        {
            var lista = eventosPorUsuario.GetValueOrDefault(c.UsuarioId, []);
            lista.Add(new FeriasCalendarioEventoDto
            {
                Tipo = FeriasCalendarioEventoTipo.Recesso,
                DataInicio = c.RecessoCorporativo.DataInicio,
                DataFim = c.RecessoCorporativo.DataFim,
                Descricao = c.RecessoCorporativo.Nome,
            });
            eventosPorUsuario[c.UsuarioId] = lista;
        }

        return usuarios
            .Select(u => new FeriasCalendarioUsuarioDto
            {
                UsuarioId = u.Id,
                UsuarioNome = u.Nome,
                Eventos = eventosPorUsuario.GetValueOrDefault(u.Id, []).OrderBy(e => e.DataInicio).ToList(),
            })
            .Where(u => u.Eventos.Count > 0)
            .ToList();
    }

    // Estrutura de alertas da Fase 5 - reaproveita a mesma lista de "Pendências" já usada no
    // Dashboard geral (Tarefa/Licença/Equipamento/Medição), em vez de criar uma tela de alertas
    // nova do zero (mesma orientação do plano do módulo). 4 tipos, todos com Origem="Férias":
    // saldo negativo, concessivo perto de vencer, saldo sem nenhuma programação criada ainda, e
    // conflito de setor (2+ colaboradores do mesmo setor com férias aprovadas sobrepostas).
    public async Task<List<PendenciaDto>> ObterAlertasAsync()
    {
        var hoje = Hoje();
        var alertas = new List<PendenciaDto>();

        var usuariosComFeriasAtivos = (await _context.Usuarios.Where(u => UsuarioTipo.ComModuloDeFerias.Contains(u.Tipo!)).ToListAsync())
            .Where(u => UsuarioStatus.Calcular(u, hoje) == UsuarioStatus.Ativo)
            .ToList();

        var periodoAtualPorUsuario = (await _periodoFeriasService.GetAllAsync(new PeriodoFeriasFiltroDto()))
            .GroupBy(p => p.UsuarioId)
            .ToDictionary(g => g.Key, g => g.First());

        var politicasPorTipo = (await _context.PoliticasFerias.Where(p => p.Ativa).ToListAsync())
            .Where(p => p.TipoVinculo is not null)
            .ToDictionary(p => p.TipoVinculo!, p => p);

        var programacoesAtivasPorUsuario = (await _context.ProgramacoesFerias
                .Include(p => p.PeriodoFerias)
                .Where(p => p.Status != ProgramacaoFeriasStatus.Cancelada && p.Status != ProgramacaoFeriasStatus.Reprovada)
                .ToListAsync())
            .ToLookup(p => p.PeriodoFerias.UsuarioId);

        foreach (var usuario in usuariosComFeriasAtivos)
        {
            if (!periodoAtualPorUsuario.TryGetValue(usuario.Id, out var periodo))
            {
                continue;
            }

            if (periodo.SaldoDisponivel < 0)
            {
                alertas.Add(new PendenciaDto
                {
                    Origem = "Férias",
                    Titulo = usuario.Nome,
                    Observacao = $"Saldo negativo ({periodo.SaldoDisponivel} dia(s)) - recesso ou ajuste levou o período abaixo de zero.",
                    Data = hoje,
                    DiasParaVencer = -1,
                    PeriodoFeriasId = periodo.Id,
                });
            }

            var politica = politicasPorTipo.GetValueOrDefault(usuario.Tipo!);
            if (politica is null || periodo.SaldoDisponivel <= 0)
            {
                continue;
            }

            var diasParaVencer = periodo.FimConcessivo.DayNumber - hoje.DayNumber;
            if (diasParaVencer <= politica.DiasAntecedenciaMarcacaoCompulsoria)
            {
                alertas.Add(new PendenciaDto
                {
                    Origem = "Férias",
                    Titulo = usuario.Nome,
                    Observacao = $"Concessivo perto de vencer com {periodo.SaldoDisponivel} dia(s) ainda não programado(s).",
                    Data = periodo.FimConcessivo,
                    DiasParaVencer = diasParaVencer,
                    PeriodoFeriasId = periodo.Id,
                });
            }
            else if (!programacoesAtivasPorUsuario[usuario.Id].Any())
            {
                alertas.Add(new PendenciaDto
                {
                    Origem = "Férias",
                    Titulo = usuario.Nome,
                    Observacao = $"Tem {periodo.SaldoDisponivel} dia(s) de saldo disponível e nenhuma programação de férias criada ainda.",
                    Data = periodo.FimConcessivo,
                    DiasParaVencer = diasParaVencer,
                    PeriodoFeriasId = periodo.Id,
                });
            }
        }

        var aprovadasPorSetor = (await _context.ProgramacoesFerias
                .Include(p => p.PeriodoFerias).ThenInclude(pf => pf.Usuario)
                .Where(p => p.Status == ProgramacaoFeriasStatus.Aprovada)
                .ToListAsync())
            .Where(p => p.PeriodoFerias.Usuario.SetorId is not null)
            .GroupBy(p => p.PeriodoFerias.Usuario.SetorId!.Value);

        foreach (var grupo in aprovadasPorSetor)
        {
            var programacoes = grupo.ToList();
            for (var i = 0; i < programacoes.Count; i++)
            {
                for (var j = i + 1; j < programacoes.Count; j++)
                {
                    var a = programacoes[i];
                    var b = programacoes[j];
                    if (a.PeriodoFerias.UsuarioId == b.PeriodoFerias.UsuarioId || a.DataInicio > b.DataFim || b.DataInicio > a.DataFim)
                    {
                        continue;
                    }

                    var inicioConflito = a.DataInicio > b.DataInicio ? a.DataInicio : b.DataInicio;
                    alertas.Add(new PendenciaDto
                    {
                        Origem = "Férias",
                        Titulo = $"{a.PeriodoFerias.Usuario.Nome} e {b.PeriodoFerias.Usuario.Nome}",
                        Observacao = "Férias aprovadas se sobrepõem no mesmo setor.",
                        Data = inicioConflito,
                        DiasParaVencer = inicioConflito.DayNumber - hoje.DayNumber,
                        PeriodoFeriasId = a.PeriodoFeriasId,
                    });
                }
            }
        }

        return alertas;
    }

    private DateOnly Hoje() => DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
}

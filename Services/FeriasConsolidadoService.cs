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

        var usuariosPjAtivos = (await _context.Usuarios.Where(u => u.Tipo == UsuarioTipo.Pj).ToListAsync())
            .Where(u => UsuarioStatus.Calcular(u, hoje) == UsuarioStatus.Ativo)
            .ToList();

        var periodoAtualPorUsuario = (await _periodoFeriasService.GetAllAsync(new PeriodoFeriasFiltroDto()))
            .GroupBy(p => p.UsuarioId)
            .ToDictionary(g => g.Key, g => g.First());

        var politica = await _context.PoliticasFerias.FirstOrDefaultAsync(p => p.TipoVinculo == UsuarioTipo.Pj && p.Ativa);

        var concessivosVencendo = new List<ConcessivoVencendoDto>();
        foreach (var usuario in usuariosPjAtivos)
        {
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
            ColaboradoresPj = usuariosPjAtivos.Count,
            ColaboradoresSemPeriodoGerado = usuariosPjAtivos.Count(u => !periodoAtualPorUsuario.ContainsKey(u.Id)),
            SaldoTotalDisponivel = usuariosPjAtivos.Sum(u => periodoAtualPorUsuario.GetValueOrDefault(u.Id)?.SaldoDisponivel ?? 0),
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

        var usuariosQuery = _context.Usuarios.Where(u => u.Tipo == UsuarioTipo.Pj).AsQueryable();
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

    private DateOnly Hoje() => DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
}

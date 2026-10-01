using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;

namespace SoftwareLicense.Api.Services;

public class DashboardService : IDashboardService
{
    private const int DiasAntecedenciaVencimentoContrato = 30;

    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly IRelatorioMensalLocacaoService _relatorioMensalLocacaoService;
    private readonly ITarefaOcorrenciaService _tarefaOcorrenciaService;
    private readonly IFeriasConsolidadoService _feriasConsolidadoService;

    public DashboardService(
        AppDbContext context,
        TimeProvider timeProvider,
        IRelatorioMensalLocacaoService relatorioMensalLocacaoService,
        ITarefaOcorrenciaService tarefaOcorrenciaService,
        IFeriasConsolidadoService feriasConsolidadoService)
    {
        _context = context;
        _timeProvider = timeProvider;
        _relatorioMensalLocacaoService = relatorioMensalLocacaoService;
        _tarefaOcorrenciaService = tarefaOcorrenciaService;
        _feriasConsolidadoService = feriasConsolidadoService;
    }

    public async Task<DashboardDto> ObterAsync()
    {
        var hoje = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

        var usuarios = await _context.Usuarios.ToListAsync();
        var usuariosAtivos = usuarios.Count(u => UsuarioStatus.Calcular(u, hoje) == UsuarioStatus.Ativo);

        var licencas = await _context.Licencas.ToListAsync();

        var usuarioLicencasAtivas = await _context.UsuarioLicencas.Where(m => m.DataFim == null).ToListAsync();
        var emUsoPorLicencaId = usuarioLicencasAtivas
            .GroupBy(m => m.LicencaId)
            .ToDictionary(g => g.Key, g => g.Count());

        // Agrupado por nome (não por Id) — pode haver mais de uma Licenca com o mesmo nome (ex.: lotes
        // de compra diferentes), e nesse caso as quantidades devem somar numa única linha, mesmo
        // espírito do agrupamento por tipo já usado pros cards de Equipamentos.
        var licencasEmUsoPorNome = licencas
            .GroupBy(l => l.Nome)
            .Select(g => new LicencaContagemPorNomeDto { Nome = g.Key, Quantidade = g.Sum(l => emUsoPorLicencaId.GetValueOrDefault(l.Id, 0)) })
            .Where(l => l.Quantidade > 0)
            .OrderBy(l => l.Nome)
            .ToList();

        var licencasDisponiveisPorNome = licencas
            .Where(l => l.Ativa)
            .GroupBy(l => l.Nome)
            .Select(g => new LicencaContagemPorNomeDto { Nome = g.Key, Quantidade = g.Sum(l => l.QuantidadeTotal - emUsoPorLicencaId.GetValueOrDefault(l.Id, 0)) })
            .Where(l => l.Quantidade > 0)
            .OrderBy(l => l.Nome)
            .ToList();

        var proximosVencimentos = licencas
            .Where(l => l.Ativa && l.DataTerminoPrevisto <= hoje.AddDays(l.DiasAntecedenciaAviso))
            .OrderBy(l => l.DataTerminoPrevisto)
            .Take(10)
            .Select(l => new VencimentoDto
            {
                LicencaId = l.Id,
                Nome = l.Nome,
                DataTerminoPrevisto = l.DataTerminoPrevisto,
                DiasParaVencer = l.DataTerminoPrevisto.DayNumber - hoje.DayNumber,
            })
            .ToList();

        var equipamentos = await _context.Equipamentos.Include(e => e.TipoEquipamento).ToListAsync();
        var idsEquipamentosAlocados = new HashSet<int>(
            await _context.EquipamentoAlocacoes.Where(a => a.DataFim == null).Select(a => a.EquipamentoId).ToListAsync());

        // Só entram equipamentos Locados (os que têm custo mensal) - Comprado não tem valor a acompanhar aqui.
        var equipamentosLocados = equipamentos.Where(e => e.Origem == EquipamentoOrigem.Locado).ToList();

        var equipamentosEmUsoPorTipo = AgruparPorTipo(equipamentosLocados.Where(e => idsEquipamentosAlocados.Contains(e.Id)));
        var equipamentosDisponiveisPorTipo = AgruparPorTipo(
            equipamentosLocados.Where(e => e.Status == EquipamentoStatus.Disponivel && !idsEquipamentosAlocados.Contains(e.Id)));
        var equipamentosLocadosAtivosPorTipo = AgruparPorTipo(equipamentosLocados.Where(e => e.Status != EquipamentoStatus.Baixado));

        var relatorioMesAtual = await _relatorioMensalLocacaoService.GerarAsync(new RelatorioMensalLocacaoFiltroDto { Ano = hoje.Year, Mes = hoje.Month });

        var proximosVencimentosContratos = equipamentos
            .Where(e => e.Origem == EquipamentoOrigem.Locado
                && e.Status != EquipamentoStatus.Baixado
                && e.DataFimContrato is not null
                && e.DataFimContrato <= hoje.AddDays(DiasAntecedenciaVencimentoContrato))
            .OrderBy(e => e.DataFimContrato)
            .Take(10)
            .Select(e => new VencimentoContratoDto
            {
                EquipamentoId = e.Id,
                Descricao = EquipamentoDescricaoHelper.Descrever(e),
                DataFimContrato = e.DataFimContrato!.Value,
                DiasParaVencer = e.DataFimContrato.Value.DayNumber - hoje.DayNumber,
            })
            .ToList();

        var alertasFerias = await _feriasConsolidadoService.ObterAlertasAsync();
        var tarefasPendentes = (await _tarefaOcorrenciaService.ObterAgendaAsync()).Take(10).ToList();

        var pendencias = MontarPendencias(tarefasPendentes, proximosVencimentos, proximosVencimentosContratos, alertasFerias);

        return new DashboardDto
        {
            UsuariosAtivos = usuariosAtivos,
            LicencasEmUsoPorNome = licencasEmUsoPorNome,
            LicencasDisponiveisPorNome = licencasDisponiveisPorNome,
            EquipamentosEmUsoPorTipo = equipamentosEmUsoPorTipo,
            EquipamentosDisponiveisPorTipo = equipamentosDisponiveisPorTipo,
            EquipamentosLocadosAtivosPorTipo = equipamentosLocadosAtivosPorTipo,
            CustoMensalLocacaoAtual = relatorioMesAtual.TotalGeral,
            Pendencias = pendencias,
        };
    }

    // Junta tarefas da Agenda (que já inclui o lembrete de medição, quando aplicável — ver
    // TarefaOcorrenciaService.GarantirOcorrenciasDeMedicaoAsync) + os alertas calculados (licença,
    // contrato de locação de equipamento) numa única lista, ordenada por urgência — mesmo espírito
    // de "o que precisa da minha atenção", só que num lugar só, com a mesma aparência de tabela.
    private static List<PendenciaDto> MontarPendencias(
        List<TarefaOcorrenciaDto> tarefasPendentes, List<VencimentoDto> proximosVencimentos,
        List<VencimentoContratoDto> proximosVencimentosContratos, List<PendenciaDto> alertasFerias)
    {
        var pendencias = new List<PendenciaDto>();

        pendencias.AddRange(tarefasPendentes.Select(t => new PendenciaDto
        {
            Origem = "Tarefa",
            Titulo = t.Titulo,
            Observacao = t.Observacao,
            Data = t.DataPrevistaAtual,
            DiasParaVencer = t.DiasParaVencer,
            TarefaOcorrenciaId = t.Id,
            ContratoId = t.ContratoId,
        }));

        pendencias.AddRange(proximosVencimentos.Select(v => new PendenciaDto
        {
            Origem = "Licença",
            Titulo = v.Nome,
            Observacao = "Licença vencendo",
            Data = v.DataTerminoPrevisto,
            DiasParaVencer = v.DiasParaVencer,
            LicencaId = v.LicencaId,
        }));

        pendencias.AddRange(proximosVencimentosContratos.Select(v => new PendenciaDto
        {
            Origem = "Equipamento",
            Titulo = v.Descricao,
            Observacao = "Contrato de locação vencendo",
            Data = v.DataFimContrato,
            DiasParaVencer = v.DiasParaVencer,
            EquipamentoId = v.EquipamentoId,
        }));

        pendencias.AddRange(alertasFerias);

        return pendencias.OrderBy(p => p.DiasParaVencer).Take(15).ToList();
    }

    private static List<EquipamentoContagemPorTipoDto> AgruparPorTipo(IEnumerable<Equipamento> equipamentos) =>
        equipamentos
            .GroupBy(e => e.TipoEquipamento.Nome)
            .OrderBy(g => g.Key)
            .Select(g => new EquipamentoContagemPorTipoDto { TipoEquipamentoNome = g.Key, Quantidade = g.Count() })
            .ToList();
}

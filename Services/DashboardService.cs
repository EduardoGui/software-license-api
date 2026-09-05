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

    public DashboardService(
        AppDbContext context,
        TimeProvider timeProvider,
        IRelatorioMensalLocacaoService relatorioMensalLocacaoService,
        ITarefaOcorrenciaService tarefaOcorrenciaService)
    {
        _context = context;
        _timeProvider = timeProvider;
        _relatorioMensalLocacaoService = relatorioMensalLocacaoService;
        _tarefaOcorrenciaService = tarefaOcorrenciaService;
    }

    public async Task<DashboardDto> ObterAsync()
    {
        var hoje = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

        var usuarios = await _context.Usuarios.ToListAsync();
        var usuariosAtivos = usuarios.Count(u => UsuarioStatus.Calcular(u, hoje) == UsuarioStatus.Ativo);

        var licencas = await _context.Licencas.ToListAsync();
        var licencasAdquiridas = licencas.Sum(l => l.QuantidadeTotal);
        var licencasEmUso = await _context.UsuarioLicencas.CountAsync(m => m.DataFim == null);
        var licencasDisponiveis = licencasAdquiridas - licencasEmUso;

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

        var alertasMedicao = await ObterAlertasMedicaoAsync(hoje);
        var tarefasPendentes = (await _tarefaOcorrenciaService.ObterAgendaAsync()).Take(10).ToList();

        var pendencias = MontarPendencias(tarefasPendentes, proximosVencimentos, proximosVencimentosContratos, alertasMedicao);

        return new DashboardDto
        {
            UsuariosAtivos = usuariosAtivos,
            LicencasDisponiveis = licencasDisponiveis,
            LicencasEmUsoPorNome = licencasEmUsoPorNome,
            LicencasDisponiveisPorNome = licencasDisponiveisPorNome,
            EquipamentosEmUsoPorTipo = equipamentosEmUsoPorTipo,
            EquipamentosDisponiveisPorTipo = equipamentosDisponiveisPorTipo,
            EquipamentosLocadosAtivosPorTipo = equipamentosLocadosAtivosPorTipo,
            CustoMensalLocacaoAtual = relatorioMesAtual.TotalGeral,
            Pendencias = pendencias,
        };
    }

    // Junta tarefas da Agenda + os 3 alertas calculados (licença, contrato de locação de
    // equipamento, medição) numa única lista, ordenada por urgência — mesmo espírito de "o que
    // precisa da minha atenção", só que num lugar só, com a mesma aparência de tabela.
    private static List<PendenciaDto> MontarPendencias(
        List<TarefaOcorrenciaDto> tarefasPendentes, List<VencimentoDto> proximosVencimentos,
        List<VencimentoContratoDto> proximosVencimentosContratos, List<AlertaMedicaoDto> alertasMedicao)
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

        pendencias.AddRange(alertasMedicao.Select(a => new PendenciaDto
        {
            Origem = "Medição",
            Titulo = a.ContratoNumero,
            Observacao = $"Fornecedor: {a.FornecedorNome}",
            Data = a.PeriodoFim,
            DiasParaVencer = a.DiasParaVencer,
            ContratoId = a.ContratoId,
        }));

        return pendencias.OrderBy(p => p.DiasParaVencer).Take(15).ToList();
    }

    // Alerta de medição: pra cada contrato Ativo cuja configuração de medição exige BM e tem
    // "dias de antecedência do alerta" preenchido, calcula o fim do período de medição corrente
    // (baseado em DiaFimPeriodo, rolando pro mês seguinte se hoje já passou do fim deste mês) e
    // avisa quando esse fim estiver a N dias ou menos (negativo = já vencido) — mas só se ainda não
    // existe nenhum BM (de qualquer status) criado pra esse período.
    private async Task<List<AlertaMedicaoDto>> ObterAlertasMedicaoAsync(DateOnly hoje)
    {
        var configs = await _context.ContratoMedicaoConfigs
            .Where(c => c.ExigeBm && c.DiasAntecedenciaAlerta != null && c.DiaFimPeriodo != null)
            .ToListAsync();

        if (configs.Count == 0)
        {
            return [];
        }

        var contratoIds = configs.Select(c => c.ContratoId).ToList();
        var contratos = await _context.Contratos
            .Include(c => c.Fornecedor)
            .Where(c => contratoIds.Contains(c.Id) && c.Status == ContratoStatus.Ativo)
            .ToDictionaryAsync(c => c.Id);

        var periodosComBm = (await _context.MedicaoBms
                .Where(m => contratoIds.Contains(m.ContratoId))
                .Select(m => new { m.ContratoId, m.PeriodoFim })
                .ToListAsync())
            .Select(m => (m.ContratoId, m.PeriodoFim))
            .ToHashSet();

        var alertas = new List<AlertaMedicaoDto>();

        foreach (var config in configs)
        {
            if (!contratos.TryGetValue(config.ContratoId, out var contrato))
            {
                continue;
            }

            var diaFimPeriodo = config.DiaFimPeriodo!.Value;
            var periodoFimAtual = FimDoPeriodoCorrente(hoje, diaFimPeriodo);
            var diasParaVencer = periodoFimAtual.DayNumber - hoje.DayNumber;

            if (diasParaVencer > config.DiasAntecedenciaAlerta!.Value)
            {
                continue;
            }

            if (periodosComBm.Contains((contrato.Id, periodoFimAtual)))
            {
                continue;
            }

            alertas.Add(new AlertaMedicaoDto
            {
                ContratoId = contrato.Id,
                ContratoNumero = contrato.Numero,
                FornecedorNome = contrato.Fornecedor.Nome,
                PeriodoFim = periodoFimAtual,
                DiasParaVencer = diasParaVencer,
            });
        }

        return alertas.OrderBy(a => a.DiasParaVencer).Take(10).ToList();
    }

    private static DateOnly FimDoPeriodoCorrente(DateOnly hoje, int diaFimPeriodo)
    {
        var fim = ClampAoMes(hoje.Year, hoje.Month, diaFimPeriodo);
        if (hoje > fim)
        {
            var proximoMes = hoje.AddMonths(1);
            fim = ClampAoMes(proximoMes.Year, proximoMes.Month, diaFimPeriodo);
        }

        return fim;
    }

    private static DateOnly ClampAoMes(int ano, int mes, int dia) =>
        new(ano, mes, Math.Min(dia, DateTime.DaysInMonth(ano, mes)));

    private static List<EquipamentoContagemPorTipoDto> AgruparPorTipo(IEnumerable<Equipamento> equipamentos) =>
        equipamentos
            .GroupBy(e => e.TipoEquipamento.Nome)
            .OrderBy(g => g.Key)
            .Select(g => new EquipamentoContagemPorTipoDto { TipoEquipamentoNome = g.Key, Quantidade = g.Count() })
            .ToList();
}

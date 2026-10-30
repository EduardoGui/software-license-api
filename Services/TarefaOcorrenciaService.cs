using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class TarefaOcorrenciaService : ITarefaOcorrenciaService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TarefaOcorrenciaService> _logger;

    public TarefaOcorrenciaService(AppDbContext context, TimeProvider timeProvider, ILogger<TarefaOcorrenciaService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    // Não existe agendador/job rodando sozinho neste projeto — as ocorrências que faltam (do mês
    // atual, ou de meses anteriores se o sistema ficou um tempo sem ser aberto) são geradas aqui,
    // sob demanda, sempre que a agenda é consultada. Idempotente: só cria o que ainda não existe.
    public async Task GarantirOcorrenciasDoMesAsync()
    {
        var hoje = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
        var mesAtual = new DateOnly(hoje.Year, hoje.Month, 1);

        var tarefasAtivas = await _context.TarefasRecorrentes.Where(t => t.Ativa).ToListAsync();
        if (tarefasAtivas.Count == 0)
        {
            return;
        }

        var tarefaIds = tarefasAtivas.Select(t => t.Id).ToList();
        var ultimoMesPorTarefa = await _context.TarefaOcorrencias
            .Where(o => o.TarefaRecorrenteId != null && tarefaIds.Contains(o.TarefaRecorrenteId.Value))
            .GroupBy(o => o.TarefaRecorrenteId!.Value)
            .Select(g => new { TarefaRecorrenteId = g.Key, UltimoMes = g.Max(o => o.MesReferencia) })
            .ToDictionaryAsync(x => x.TarefaRecorrenteId, x => x.UltimoMes);

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var novasOcorrencias = new List<TarefaOcorrencia>();

        foreach (var tarefa in tarefasAtivas)
        {
            var proximoMes = ultimoMesPorTarefa.TryGetValue(tarefa.Id, out var ultimoMes)
                ? ultimoMes.AddMonths(1)
                : mesAtual;

            // Limite de segurança: nunca gera mais de 24 meses de atraso de uma vez só.
            var meses = 0;
            while (proximoMes <= mesAtual && meses < 24)
            {
                var dataPrevista = ClampAoMes(proximoMes.Year, proximoMes.Month, tarefa.DiaDoMes);
                novasOcorrencias.Add(new TarefaOcorrencia
                {
                    TarefaRecorrenteId = tarefa.Id,
                    Titulo = tarefa.Titulo,
                    MesReferencia = proximoMes,
                    DataPrevistaOriginal = dataPrevista,
                    DataPrevistaAtual = dataPrevista,
                    Status = TarefaOcorrenciaStatus.Pendente,
                    DataCriacao = agora,
                    DataAtualizacao = agora,
                });

                proximoMes = proximoMes.AddMonths(1);
                meses++;
            }
        }

        if (novasOcorrencias.Count > 0)
        {
            _context.TarefaOcorrencias.AddRange(novasOcorrencias);
            await _context.SaveChangesAsync();

            _logger.LogInformation("{Quantidade} ocorrência(s) de tarefa geradas", novasOcorrencias.Count);
        }
    }

    // Lembrete de "iniciar medição", gerado a partir da configuração de medição de cada contrato
    // (ContratoMedicaoConfig.DiaFimPeriodo/DiasAntecedenciaAlerta) — mesmo gatilho que antes só
    // alimentava um alerta calculado no Dashboard (ver histórico em DashboardService), agora
    // materializado como uma TarefaOcorrencia de verdade (ContratoId preenchido marca a origem).
    // Sob demanda, igual a GarantirOcorrenciasDoMesAsync: roda sempre que a agenda é consultada.
    // O fim real de uma BM nem sempre cai no "dia fim" configurado (mês de 30 x 31 dias, primeiro período parcial):
    // uma BM que termina até esta tolerância antes do fim esperado já cobre o período.
    private const int ToleranciaFimBmDias = 5;

    public async Task GarantirOcorrenciasDeMedicaoAsync()
    {
        var hoje = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

        var configs = await _context.ContratoMedicaoConfigs
            .Where(c => c.ExigeBm && c.DiasAntecedenciaAlerta != null && c.DiaFimPeriodo != null)
            .ToListAsync();

        if (configs.Count == 0)
        {
            return;
        }

        var contratoIds = configs.Select(c => c.ContratoId).ToList();
        var contratos = await _context.Contratos
            .Include(c => c.Fornecedor)
            .Where(c => contratoIds.Contains(c.Id) && c.Status == ContratoStatus.Ativo)
            .ToDictionaryAsync(c => c.Id);

        var bms = await _context.MedicaoBms
            .Where(m => contratoIds.Contains(m.ContratoId))
            .Select(m => new { m.ContratoId, m.PeriodoFim })
            .ToListAsync();
        var periodosComBm = bms.Select(m => (m.ContratoId, m.PeriodoFim)).ToHashSet();
        var ultimoFimComBm = bms.GroupBy(m => m.ContratoId).ToDictionary(g => g.Key, g => g.Max(m => m.PeriodoFim));

        var ocorrenciasExistentes = (await _context.TarefaOcorrencias
                .Where(o => o.ContratoId != null && contratoIds.Contains(o.ContratoId.Value))
                .Select(o => new { o.ContratoId, o.MesReferencia })
                .ToListAsync())
            .Select(o => (o.ContratoId!.Value, o.MesReferencia))
            .ToHashSet();

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var novasOcorrencias = new List<TarefaOcorrencia>();

        // Uma ocorrência por contrato/mês de referência (inclusive as já concluídas): nunca duplica.
        void AdicionarLembrete(Contrato contrato, DateOnly periodoFim, int antecedencia, bool atrasada)
        {
            var mesReferencia = new DateOnly(periodoFim.Year, periodoFim.Month, 1);
            if (!ocorrenciasExistentes.Add((contrato.Id, mesReferencia)))
            {
                return;
            }

            var dataPrevista = periodoFim.AddDays(-antecedencia);
            novasOcorrencias.Add(new TarefaOcorrencia
            {
                ContratoId = contrato.Id,
                Titulo = $"{(atrasada ? "Medição atrasada" : "Iniciar medição")} — {contrato.Fornecedor.Nome}",
                MesReferencia = mesReferencia,
                DataPrevistaOriginal = dataPrevista,
                DataPrevistaAtual = dataPrevista,
                Status = TarefaOcorrenciaStatus.Pendente,
                Observacao = $"Contrato {contrato.Numero} • período até {periodoFim:dd/MM/yyyy}{(atrasada ? " • ainda sem medição lançada" : string.Empty)}",
                DataCriacao = agora,
                DataAtualizacao = agora,
            });
        }

        foreach (var config in configs)
        {
            if (!contratos.TryGetValue(config.ContratoId, out var contrato))
            {
                continue;
            }

            var antecedencia = config.DiasAntecedenciaAlerta!.Value;
            var periodoFimAtual = PeriodoMedicaoHelper.FimDoPeriodoCorrente(hoje, config.DiaFimPeriodo!.Value);
            var diasParaVencerPeriodo = periodoFimAtual.DayNumber - hoje.DayNumber;

            // Período corrente: avisa quando faltam até "antecedência" dias para o fim e ainda não há BM dele.
            if (diasParaVencerPeriodo <= antecedencia && !periodosComBm.Contains((contrato.Id, periodoFimAtual)))
            {
                AdicionarLembrete(contrato, periodoFimAtual, antecedencia, atrasada: false);
            }

            // Recuperação: o lembrete só nasce quando alguém abre a Agenda/Dashboard dentro da janela. Se ninguém abriu
            // e o período já encerrou sem BM, avisa como atrasada em vez de perder o lembrete de vez.
            var periodoFimAnterior = PeriodoMedicaoHelper.FimDoPeriodoAnterior(periodoFimAtual, config.DiaFimPeriodo!.Value);
            if (periodoFimAnterior >= contrato.DataInicioVigencia
                && !periodosComBm.Contains((contrato.Id, periodoFimAnterior))
                && !(ultimoFimComBm.TryGetValue(contrato.Id, out var ultimoFim) && ultimoFim >= periodoFimAnterior.AddDays(-ToleranciaFimBmDias)))
            {
                AdicionarLembrete(contrato, periodoFimAnterior, antecedencia, atrasada: true);
            }
        }

        if (novasOcorrencias.Count > 0)
        {
            _context.TarefaOcorrencias.AddRange(novasOcorrencias);
            await _context.SaveChangesAsync();

            _logger.LogInformation("{Quantidade} lembrete(s) de medição gerado(s)", novasOcorrencias.Count);
        }
    }

    public async Task<List<TarefaOcorrenciaDto>> ObterAgendaAsync()
    {
        await GarantirOcorrenciasDoMesAsync();
        await GarantirOcorrenciasDeMedicaoAsync();

        var hoje = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

        var ocorrencias = await _context.TarefaOcorrencias
            .Where(o => o.Status == TarefaOcorrenciaStatus.Pendente)
            .OrderBy(o => o.DataPrevistaAtual)
            .ToListAsync();

        return ocorrencias.Select(o => ParaDto(o, hoje)).ToList();
    }

    // Tarefa única: sem regra recorrente por trás, só uma ocorrência criada na hora, com a data
    // já escolhida — não passa por GarantirOcorrenciasDoMesAsync (não há "próximo mês" pra gerar).
    public async Task<TarefaOcorrenciaDto> CriarTarefaUnicaAsync(CreateTarefaUnicaDto dto)
    {
        var agora = _timeProvider.GetUtcNow().UtcDateTime;

        var ocorrencia = new TarefaOcorrencia
        {
            TarefaRecorrenteId = null,
            Titulo = dto.Titulo.Trim(),
            MesReferencia = new DateOnly(dto.Data.Year, dto.Data.Month, 1),
            DataPrevistaOriginal = dto.Data,
            DataPrevistaAtual = dto.Data,
            Status = TarefaOcorrenciaStatus.Pendente,
            Observacao = string.IsNullOrWhiteSpace(dto.Observacao) ? null : dto.Observacao.Trim(),
            DataCriacao = agora,
            DataAtualizacao = agora,
        };

        _context.TarefaOcorrencias.Add(ocorrencia);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Tarefa única {OcorrenciaId} criada para {Data}", ocorrencia.Id, dto.Data);

        return ParaDto(ocorrencia, DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime));
    }

    public async Task<TarefaOcorrenciaDto> ConcluirAsync(int ocorrenciaId)
    {
        var ocorrencia = await BuscarOuFalhar(ocorrenciaId);

        if (ocorrencia.Status == TarefaOcorrenciaStatus.Concluida)
        {
            throw new BusinessRuleException("Essa ocorrência já foi concluída.");
        }

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        ocorrencia.Status = TarefaOcorrenciaStatus.Concluida;
        ocorrencia.DataConclusao = agora;
        ocorrencia.DataAtualizacao = agora;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Ocorrência {OcorrenciaId} concluída", ocorrencia.Id);

        return ParaDto(ocorrencia, DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime));
    }

    public async Task<TarefaOcorrenciaDto> EditarAsync(int ocorrenciaId, EditarTarefaOcorrenciaDto dto)
    {
        var ocorrencia = await BuscarOuFalhar(ocorrenciaId);

        if (ocorrencia.Status == TarefaOcorrenciaStatus.Concluida)
        {
            throw new BusinessRuleException("Não é possível editar uma ocorrência já concluída.");
        }

        ocorrencia.Titulo = dto.Titulo.Trim();
        ocorrencia.DataPrevistaAtual = dto.NovaData;
        ocorrencia.Observacao = string.IsNullOrWhiteSpace(dto.Observacao) ? null : dto.Observacao.Trim();
        ocorrencia.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Ocorrência {OcorrenciaId} editada (nova data {NovaData})", ocorrencia.Id, dto.NovaData);

        return ParaDto(ocorrencia, DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime));
    }

    public async Task<TarefaOcorrenciaDto> AtualizarObservacaoAsync(int ocorrenciaId, AtualizarObservacaoTarefaOcorrenciaDto dto)
    {
        var ocorrencia = await BuscarOuFalhar(ocorrenciaId);

        ocorrencia.Observacao = string.IsNullOrWhiteSpace(dto.Observacao) ? null : dto.Observacao.Trim();
        ocorrencia.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Observação da ocorrência {OcorrenciaId} atualizada", ocorrencia.Id);

        return ParaDto(ocorrencia, DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime));
    }

    private async Task<TarefaOcorrencia> BuscarOuFalhar(int id)
    {
        var ocorrencia = await _context.TarefaOcorrencias
            .FirstOrDefaultAsync(o => o.Id == id);

        if (ocorrencia is null)
        {
            throw new NotFoundException($"Ocorrência {id} não encontrada.");
        }

        return ocorrencia;
    }

    private static DateOnly ClampAoMes(int ano, int mes, int dia) =>
        new(ano, mes, Math.Min(dia, DateTime.DaysInMonth(ano, mes)));

    private static TarefaOcorrenciaDto ParaDto(TarefaOcorrencia o, DateOnly hoje) => new()
    {
        Id = o.Id,
        TarefaRecorrenteId = o.TarefaRecorrenteId,
        ContratoId = o.ContratoId,
        Titulo = o.Titulo,
        DataPrevistaOriginal = o.DataPrevistaOriginal,
        DataPrevistaAtual = o.DataPrevistaAtual,
        Status = o.Status,
        DataConclusao = o.DataConclusao,
        Observacao = o.Observacao,
        DiasParaVencer = o.DataPrevistaAtual.DayNumber - hoje.DayNumber,
    };
}

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

    public async Task<List<TarefaOcorrenciaDto>> ObterAgendaAsync()
    {
        await GarantirOcorrenciasDoMesAsync();

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

    public async Task<TarefaOcorrenciaDto> AdiarAsync(int ocorrenciaId, AdiarTarefaOcorrenciaDto dto)
    {
        var ocorrencia = await BuscarOuFalhar(ocorrenciaId);

        if (ocorrencia.Status == TarefaOcorrenciaStatus.Concluida)
        {
            throw new BusinessRuleException("Não é possível adiar uma ocorrência já concluída.");
        }

        ocorrencia.DataPrevistaAtual = dto.NovaData;
        ocorrencia.Observacao = string.IsNullOrWhiteSpace(dto.Observacao) ? null : dto.Observacao.Trim();
        ocorrencia.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Ocorrência {OcorrenciaId} adiada para {NovaData}", ocorrencia.Id, dto.NovaData);

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
        Titulo = o.Titulo,
        DataPrevistaOriginal = o.DataPrevistaOriginal,
        DataPrevistaAtual = o.DataPrevistaAtual,
        Status = o.Status,
        DataConclusao = o.DataConclusao,
        Observacao = o.Observacao,
        DiasParaVencer = o.DataPrevistaAtual.DayNumber - hoje.DayNumber,
    };
}

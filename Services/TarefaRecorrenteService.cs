using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class TarefaRecorrenteService : ITarefaRecorrenteService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TarefaRecorrenteService> _logger;

    public TarefaRecorrenteService(AppDbContext context, TimeProvider timeProvider, ILogger<TarefaRecorrenteService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<List<TarefaRecorrenteDto>> GetAllAsync(TarefaRecorrenteFiltroDto filtro)
    {
        var query = _context.TarefasRecorrentes.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Titulo))
        {
            query = query.Where(t => EF.Functions.ILike(t.Titulo, $"%{filtro.Titulo}%"));
        }

        if (filtro.Ativa is not null)
        {
            query = query.Where(t => t.Ativa == filtro.Ativa);
        }

        var tarefas = await query.OrderBy(t => t.DiaDoMes).ThenBy(t => t.Titulo).ToListAsync();
        return tarefas.Select(ParaDto).ToList();
    }

    public async Task<TarefaRecorrenteDto> GetByIdAsync(int id)
    {
        var tarefa = await BuscarOuFalhar(id);
        return ParaDto(tarefa);
    }

    public async Task<TarefaRecorrenteDto> CreateAsync(CreateTarefaRecorrenteDto dto)
    {
        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var tarefa = new TarefaRecorrente
        {
            Titulo = dto.Titulo.Trim(),
            DiaDoMes = dto.DiaDoMes,
            Observacao = string.IsNullOrWhiteSpace(dto.Observacao) ? null : dto.Observacao.Trim(),
            Ativa = dto.Ativa,
            DataCriacao = agora,
            DataAtualizacao = agora,
        };

        _context.TarefasRecorrentes.Add(tarefa);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Tarefa recorrente {TarefaId} criada", tarefa.Id);

        return ParaDto(tarefa);
    }

    public async Task<TarefaRecorrenteDto> UpdateAsync(int id, UpdateTarefaRecorrenteDto dto)
    {
        var tarefa = await BuscarOuFalhar(id);

        tarefa.Titulo = dto.Titulo.Trim();
        tarefa.DiaDoMes = dto.DiaDoMes;
        tarefa.Observacao = string.IsNullOrWhiteSpace(dto.Observacao) ? null : dto.Observacao.Trim();
        tarefa.Ativa = dto.Ativa;
        tarefa.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Tarefa recorrente {TarefaId} atualizada", tarefa.Id);

        return ParaDto(tarefa);
    }

    private async Task<TarefaRecorrente> BuscarOuFalhar(int id)
    {
        var tarefa = await _context.TarefasRecorrentes.FindAsync(id);
        if (tarefa is null)
        {
            throw new NotFoundException($"Tarefa recorrente {id} não encontrada.");
        }

        return tarefa;
    }

    private static TarefaRecorrenteDto ParaDto(TarefaRecorrente t) => new()
    {
        Id = t.Id,
        Titulo = t.Titulo,
        DiaDoMes = t.DiaDoMes,
        Observacao = t.Observacao,
        Ativa = t.Ativa,
        DataCriacao = t.DataCriacao,
        DataAtualizacao = t.DataAtualizacao,
    };
}

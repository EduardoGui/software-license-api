using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;

namespace SoftwareLicense.Api.Services;

public class AuditoriaService : IAuditoriaService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;

    public AuditoriaService(AppDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task RegistrarAsync(int? usuarioId, string entidade, int entidadeId, string acao, string? detalhe = null)
    {
        var usuarioNome = usuarioId is null
            ? "Administrador"
            : await _context.Usuarios.Where(u => u.Id == usuarioId).Select(u => u.Nome).FirstOrDefaultAsync() ?? "Desconhecido";

        _context.LogsAuditoria.Add(new LogAuditoria
        {
            DataHora = _timeProvider.GetUtcNow().UtcDateTime,
            UsuarioId = usuarioId,
            UsuarioNome = usuarioNome,
            Entidade = entidade,
            EntidadeId = entidadeId,
            Acao = acao,
            Detalhe = detalhe,
        });

        await _context.SaveChangesAsync();
    }

    public async Task<List<LogAuditoriaDto>> GetAllAsync(LogAuditoriaFiltroDto filtro)
    {
        var query = _context.LogsAuditoria.AsQueryable();

        if (filtro.DataInicial is not null)
        {
            var inicio = DateTime.SpecifyKind(filtro.DataInicial.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            query = query.Where(l => l.DataHora >= inicio);
        }

        if (filtro.DataFinal is not null)
        {
            var fim = DateTime.SpecifyKind(filtro.DataFinal.Value.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);
            query = query.Where(l => l.DataHora <= fim);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Entidade))
        {
            query = query.Where(l => l.Entidade == filtro.Entidade);
        }

        if (filtro.EntidadeId is not null)
        {
            query = query.Where(l => l.EntidadeId == filtro.EntidadeId);
        }

        if (filtro.UsuarioId is not null)
        {
            query = query.Where(l => l.UsuarioId == filtro.UsuarioId);
        }

        return await query
            .OrderByDescending(l => l.DataHora)
            .Select(l => new LogAuditoriaDto
            {
                Id = l.Id,
                DataHora = l.DataHora,
                UsuarioId = l.UsuarioId,
                UsuarioNome = l.UsuarioNome,
                Entidade = l.Entidade,
                EntidadeId = l.EntidadeId,
                Acao = l.Acao,
                Detalhe = l.Detalhe,
            })
            .ToListAsync();
    }
}

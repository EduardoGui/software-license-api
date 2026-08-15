using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public class TimelineService : ITimelineService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;

    public TimelineService(AppDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<List<TimelineUsuarioDto>> ObterAsync(TimelineFiltroDto filtro)
    {
        var hoje = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

        var usuariosQuery = _context.Usuarios.AsQueryable();
        if (filtro.UsuarioId is not null)
        {
            usuariosQuery = usuariosQuery.Where(u => u.Id == filtro.UsuarioId);
        }

        if (filtro.DataFinal is not null)
        {
            usuariosQuery = usuariosQuery.Where(u => u.DataInicio <= filtro.DataFinal);
        }

        if (filtro.DataInicial is not null)
        {
            usuariosQuery = usuariosQuery.Where(u => u.DataFim == null || u.DataFim >= filtro.DataInicial);
        }

        var usuarios = await usuariosQuery.OrderBy(u => u.Nome).ToListAsync();

        var movimentacoesQuery = _context.UsuarioLicencas.Include(m => m.Licenca).AsQueryable();

        if (filtro.LicencaId is not null)
        {
            movimentacoesQuery = movimentacoesQuery.Where(m => m.LicencaId == filtro.LicencaId);
        }

        if (string.Equals(filtro.Status, MovimentacaoStatus.EmUso, StringComparison.OrdinalIgnoreCase))
        {
            movimentacoesQuery = movimentacoesQuery.Where(m => m.DataFim == null);
        }
        else if (string.Equals(filtro.Status, MovimentacaoStatus.Encerrado, StringComparison.OrdinalIgnoreCase))
        {
            movimentacoesQuery = movimentacoesQuery.Where(m => m.DataFim != null);
        }

        if (filtro.DataFinal is not null)
        {
            movimentacoesQuery = movimentacoesQuery.Where(m => m.DataInicio <= filtro.DataFinal);
        }

        if (filtro.DataInicial is not null)
        {
            movimentacoesQuery = movimentacoesQuery.Where(m => m.DataFim == null || m.DataFim >= filtro.DataInicial);
        }

        var movimentacoes = await movimentacoesQuery.ToListAsync();
        var movimentacoesPorUsuario = movimentacoes.ToLookup(m => m.UsuarioId);

        var resultado = usuarios.Select(u => new TimelineUsuarioDto
        {
            UsuarioId = u.Id,
            UsuarioNome = u.Nome,
            DataInicio = u.DataInicio,
            DataFim = u.DataFim,
            Status = UsuarioStatus.Calcular(u, hoje),
            Licencas = movimentacoesPorUsuario[u.Id]
                .OrderBy(m => m.DataInicio)
                .Select(m => new TimelineLicencaDto
                {
                    MovimentacaoId = m.Id,
                    LicencaId = m.LicencaId,
                    LicencaNome = m.Licenca.Nome,
                    DataInicio = m.DataInicio,
                    DataFim = m.DataFim,
                    Status = MovimentacaoStatus.Calcular(m),
                    Observacao = m.Observacao,
                })
                .ToList(),
        });

        // Filtrando por licença ou status, só fazem sentido usuários com pelo menos
        // uma movimentação correspondente — caso contrário a linha ficaria vazia.
        if (filtro.LicencaId is not null || !string.IsNullOrWhiteSpace(filtro.Status))
        {
            resultado = resultado.Where(u => u.Licencas.Count > 0);
        }

        return resultado.ToList();
    }
}

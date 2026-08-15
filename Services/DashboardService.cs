using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;

    public DashboardService(AppDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
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

        return new DashboardDto
        {
            UsuariosAtivos = usuariosAtivos,
            LicencasAdquiridas = licencasAdquiridas,
            LicencasEmUso = licencasEmUso,
            LicencasDisponiveis = licencasDisponiveis,
            ProximosVencimentos = proximosVencimentos,
        };
    }
}

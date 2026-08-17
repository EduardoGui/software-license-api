using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;

namespace SoftwareLicense.Api.Services;

public class DashboardService : IDashboardService
{
    private const int DiasAntecedenciaVencimentoContrato = 30;

    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly IRelatorioMensalLocacaoService _relatorioMensalLocacaoService;

    public DashboardService(AppDbContext context, TimeProvider timeProvider, IRelatorioMensalLocacaoService relatorioMensalLocacaoService)
    {
        _context = context;
        _timeProvider = timeProvider;
        _relatorioMensalLocacaoService = relatorioMensalLocacaoService;
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

        var equipamentos = await _context.Equipamentos.Include(e => e.TipoEquipamento).ToListAsync();
        var idsEquipamentosAlocados = new HashSet<int>(
            await _context.EquipamentoAlocacoes.Where(a => a.DataFim == null).Select(a => a.EquipamentoId).ToListAsync());

        var equipamentosEmUso = idsEquipamentosAlocados.Count;
        var equipamentosDisponiveis = equipamentos.Count(e => e.Status == EquipamentoStatus.Disponivel && !idsEquipamentosAlocados.Contains(e.Id));
        var equipamentosLocadosAtivos = equipamentos.Count(e => e.Origem == EquipamentoOrigem.Locado && e.Status != EquipamentoStatus.Baixado);

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
                Descricao = DescreverEquipamento(e),
                DataFimContrato = e.DataFimContrato!.Value,
                DiasParaVencer = e.DataFimContrato.Value.DayNumber - hoje.DayNumber,
            })
            .ToList();

        return new DashboardDto
        {
            UsuariosAtivos = usuariosAtivos,
            LicencasAdquiridas = licencasAdquiridas,
            LicencasEmUso = licencasEmUso,
            LicencasDisponiveis = licencasDisponiveis,
            ProximosVencimentos = proximosVencimentos,
            EquipamentosEmUso = equipamentosEmUso,
            EquipamentosDisponiveis = equipamentosDisponiveis,
            EquipamentosLocadosAtivos = equipamentosLocadosAtivos,
            CustoMensalLocacaoAtual = relatorioMesAtual.TotalGeral,
            ProximosVencimentosContratos = proximosVencimentosContratos,
        };
    }

    private static string DescreverEquipamento(Entities.Equipamento e)
    {
        var identificador = !string.IsNullOrWhiteSpace(e.Patrimonio) ? e.Patrimonio : e.NumeroSerie;
        return string.IsNullOrWhiteSpace(identificador) ? e.TipoEquipamento.Nome : $"{e.TipoEquipamento.Nome} ({identificador})";
    }
}

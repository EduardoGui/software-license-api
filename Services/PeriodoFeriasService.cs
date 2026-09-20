using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class PeriodoFeriasService : IPeriodoFeriasService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly IAuditoriaService _auditoriaService;
    private readonly ILogger<PeriodoFeriasService> _logger;

    public PeriodoFeriasService(AppDbContext context, TimeProvider timeProvider, IAuditoriaService auditoriaService, ILogger<PeriodoFeriasService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _auditoriaService = auditoriaService;
        _logger = logger;
    }

    public async Task<List<PeriodoFeriasDto>> GetAllAsync(PeriodoFeriasFiltroDto filtro)
    {
        var query = MontarConsultaBase();

        if (filtro.UsuarioId is not null)
        {
            query = query.Where(p => p.UsuarioId == filtro.UsuarioId);
        }

        var hoje = Hoje();
        var periodos = await query.OrderByDescending(p => p.InicioAquisitivo).ToListAsync();
        return periodos.Select(p => ParaDto(p, hoje)).ToList();
    }

    public async Task<PeriodoFeriasDto> GetByIdAsync(int id)
    {
        var periodo = await BuscarOuFalhar(id);
        return ParaDto(periodo, Hoje());
    }

    public async Task<List<MovimentacaoSaldoFeriasDto>> GetMovimentacoesAsync(int periodoFeriasId)
    {
        await BuscarOuFalhar(periodoFeriasId);

        return await _context.MovimentacoesSaldoFerias
            .Where(m => m.PeriodoFeriasId == periodoFeriasId)
            .OrderByDescending(m => m.Data).ThenByDescending(m => m.Id)
            .Select(m => new MovimentacaoSaldoFeriasDto
            {
                Id = m.Id,
                Tipo = m.Tipo,
                Quantidade = m.Quantidade,
                Data = m.Data,
                UsuarioResponsavelId = m.UsuarioResponsavelId,
                UsuarioResponsavelNome = m.UsuarioResponsavelId == null ? "Administrador" : m.UsuarioResponsavel!.Nome,
                Observacao = m.Observacao,
                Anulada = m.ProgramacaoFerias != null
                    && (m.ProgramacaoFerias.Status == ProgramacaoFeriasStatus.Cancelada || m.ProgramacaoFerias.Status == ProgramacaoFeriasStatus.Reprovada),
                DataCriacao = m.DataCriacao,
            })
            .ToListAsync();
    }

    public async Task<PeriodoFeriasDto> GerarProximoPeriodoAsync(int usuarioId, int? usuarioResponsavelId)
    {
        var usuario = await _context.Usuarios.FindAsync(usuarioId);
        if (usuario is null)
        {
            throw new NotFoundException($"Usuário {usuarioId} não encontrado.");
        }

        if (usuario.Tipo != UsuarioTipo.Pj)
        {
            throw new BusinessRuleException("Este módulo está disponível apenas para colaboradores PJ no momento.");
        }

        var politica = await _context.PoliticasFerias.FirstOrDefaultAsync(p => p.TipoVinculo == UsuarioTipo.Pj && p.Ativa);
        if (politica is null)
        {
            throw new BusinessRuleException("Nenhuma política de férias ativa configurada para PJ.");
        }

        var hoje = Hoje();
        var ultimoPeriodo = await _context.PeriodosFerias
            .Include(p => p.Movimentacoes)
            .Where(p => p.UsuarioId == usuarioId)
            .OrderByDescending(p => p.FimAquisitivo)
            .FirstOrDefaultAsync();

        DateOnly inicioAquisitivo;
        if (ultimoPeriodo is null)
        {
            inicioAquisitivo = usuario.DataInicio;
        }
        else
        {
            if (hoje <= ultimoPeriodo.FimAquisitivo)
            {
                throw new BusinessRuleException(
                    $"O período aquisitivo atual ainda não terminou (termina em {ultimoPeriodo.FimAquisitivo:dd/MM/yyyy}).");
            }

            await MaterializarAquisicaoSeNecessarioAsync(ultimoPeriodo, hoje);
            inicioAquisitivo = ultimoPeriodo.FimAquisitivo.AddDays(1);
        }

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var fimAquisitivo = inicioAquisitivo.AddYears(1).AddDays(-1);
        var inicioConcessivo = fimAquisitivo.AddDays(1);
        var fimConcessivo = inicioConcessivo.AddYears(1).AddDays(-1);

        var periodo = new PeriodoFerias
        {
            UsuarioId = usuarioId,
            InicioAquisitivo = inicioAquisitivo,
            FimAquisitivo = fimAquisitivo,
            InicioConcessivo = inicioConcessivo,
            FimConcessivo = fimConcessivo,
            DiasDireito = politica.DiasDireitoPorAno,
            DataCriacao = agora,
            DataAtualizacao = agora,
        };

        _context.PeriodosFerias.Add(periodo);
        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(
            usuarioResponsavelId, LogAuditoriaEntidade.PeriodoFerias, periodo.Id, LogAuditoriaAcao.Criado,
            $"Período aquisitivo {inicioAquisitivo:dd/MM/yyyy} a {fimAquisitivo:dd/MM/yyyy} gerado para {usuario.Nome}.");

        _logger.LogInformation("Período de Férias {PeriodoFeriasId} gerado para o usuário {UsuarioId}", periodo.Id, usuarioId);

        var criado = await BuscarOuFalhar(periodo.Id);
        return ParaDto(criado, hoje);
    }

    public async Task<PeriodoFeriasDto> RegistrarAjusteManualAsync(int periodoFeriasId, CreateAjusteManualSaldoFeriasDto dto, int? usuarioResponsavelId)
    {
        if (dto.Quantidade == 0)
        {
            throw new BusinessRuleException("A quantidade do ajuste não pode ser zero.");
        }

        if (string.IsNullOrWhiteSpace(dto.Observacao))
        {
            throw new BusinessRuleException("Justificativa é obrigatória para ajuste manual de saldo.");
        }

        var periodo = await BuscarOuFalhar(periodoFeriasId);
        var hoje = Hoje();

        await MaterializarAquisicaoSeNecessarioAsync(periodo, hoje);

        _context.MovimentacoesSaldoFerias.Add(new MovimentacaoSaldoFerias
        {
            PeriodoFeriasId = periodoFeriasId,
            Tipo = MovimentacaoSaldoFeriasTipo.AjusteManual,
            Quantidade = dto.Quantidade,
            Data = hoje,
            UsuarioResponsavelId = usuarioResponsavelId,
            Observacao = dto.Observacao.Trim(),
            DataCriacao = _timeProvider.GetUtcNow().UtcDateTime,
        });
        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(
            usuarioResponsavelId, LogAuditoriaEntidade.PeriodoFerias, periodoFeriasId, LogAuditoriaAcao.Atualizado,
            $"Ajuste manual de saldo: {(dto.Quantidade > 0 ? "+" : "")}{dto.Quantidade} dia(s). {dto.Observacao.Trim()}");

        _logger.LogInformation("Ajuste manual de {Quantidade} dia(s) registrado no Período de Férias {PeriodoFeriasId}", dto.Quantidade, periodoFeriasId);

        var atualizado = await BuscarOuFalhar(periodoFeriasId);
        return ParaDto(atualizado, hoje);
    }

    private async Task MaterializarAquisicaoSeNecessarioAsync(PeriodoFerias periodo, DateOnly hoje)
    {
        if (hoje <= periodo.FimAquisitivo)
        {
            return;
        }

        var jaMaterializada = periodo.Movimentacoes.Any(m => m.Tipo == MovimentacaoSaldoFeriasTipo.Aquisicao)
            || await _context.MovimentacoesSaldoFerias.AnyAsync(m => m.PeriodoFeriasId == periodo.Id && m.Tipo == MovimentacaoSaldoFeriasTipo.Aquisicao);
        if (jaMaterializada)
        {
            return;
        }

        _context.MovimentacoesSaldoFerias.Add(new MovimentacaoSaldoFerias
        {
            PeriodoFeriasId = periodo.Id,
            Tipo = MovimentacaoSaldoFeriasTipo.Aquisicao,
            Quantidade = periodo.DiasDireito,
            Data = periodo.FimAquisitivo.AddDays(1),
            UsuarioResponsavelId = null,
            Observacao = null,
            DataCriacao = _timeProvider.GetUtcNow().UtcDateTime,
        });
        await _context.SaveChangesAsync();
    }

    private IQueryable<PeriodoFerias> MontarConsultaBase() =>
        _context.PeriodosFerias
            .Include(p => p.Usuario)
            .Include(p => p.Movimentacoes).ThenInclude(m => m.ProgramacaoFerias)
            .Include(p => p.Movimentacoes).ThenInclude(m => m.RecessoColaborador).ThenInclude(rc => rc!.RecessoCorporativo)
            .Include(p => p.ProgramacoesFerias)
            .AsQueryable();

    private async Task<PeriodoFerias> BuscarOuFalhar(int id)
    {
        var periodo = await MontarConsultaBase().FirstOrDefaultAsync(p => p.Id == id);
        if (periodo is null)
        {
            throw new NotFoundException($"Período de Férias {id} não encontrado.");
        }

        return periodo;
    }

    private DateOnly Hoje() => DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

    private static PeriodoFeriasDto ParaDto(PeriodoFerias p, DateOnly hoje)
    {
        var aquisitivoFechado = hoje > p.FimAquisitivo;
        var direitoAdquirido = aquisitivoFechado ? p.DiasDireito : 0;

        var totalDiasAquisitivo = p.FimAquisitivo.DayNumber - p.InicioAquisitivo.DayNumber + 1;
        var diasDecorridos = Math.Clamp(hoje.DayNumber - p.InicioAquisitivo.DayNumber + 1, 0, totalDiasAquisitivo);
        var projecao = aquisitivoFechado
            ? p.DiasDireito
            : Math.Round(p.DiasDireito * (decimal)diasDecorridos / totalDiasAquisitivo, 1);

        // Cancelar/reprovar uma ProgramacaoFerias não gera lançamento de reversão - a soma do saldo
        // simplesmente ignora movimentações cuja ProgramacaoFerias vinculada não está mais ativa
        // (mesmo padrão de Entrega.Status != Cancelado em CampanhaEntregaItem.SaldoDisponivel).
        var somaMovimentacoes = p.Movimentacoes
            .Where(m => m.Tipo != MovimentacaoSaldoFeriasTipo.Aquisicao)
            .Where(m => m.ProgramacaoFerias is null || (m.ProgramacaoFerias.Status != ProgramacaoFeriasStatus.Cancelada && m.ProgramacaoFerias.Status != ProgramacaoFeriasStatus.Reprovada))
            .Sum(m => m.Quantidade);
        var aquisicaoMaterializada = p.Movimentacoes.Any(m => m.Tipo == MovimentacaoSaldoFeriasTipo.Aquisicao);

        // Comprometido/Consumido vêm das MOVIMENTAÇÕES (não só de ProgramacaoFerias) - senão dias
        // debitados por Recesso Corporativo ou Abono Pecuniário ficam "invisíveis" nessas duas
        // colunas mesmo já estando descontados do Disponível (achado do usuário: recesso não
        // aparecia em nenhum dos dois). Mesma regra de corte já usada antes: data de referência no
        // futuro = Comprometido, hoje ou no passado (já em gozo ou já concluído) = Consumido.
        var comprometido = 0;
        var consumido = 0;
        foreach (var m in p.Movimentacoes)
        {
            DateOnly? dataReferencia = m.Tipo switch
            {
                MovimentacaoSaldoFeriasTipo.ProgramacaoFerias or MovimentacaoSaldoFeriasTipo.AbonoPecuniario => m.ProgramacaoFerias?.DataInicio,
                MovimentacaoSaldoFeriasTipo.Recesso => m.RecessoColaborador?.RecessoCorporativo.DataInicio,
                _ => null,
            };

            if (dataReferencia is null)
            {
                continue;
            }

            if (m.ProgramacaoFerias is not null
                && (m.ProgramacaoFerias.Status == ProgramacaoFeriasStatus.Cancelada || m.ProgramacaoFerias.Status == ProgramacaoFeriasStatus.Reprovada))
            {
                continue;
            }

            if (dataReferencia > hoje)
            {
                comprometido += -m.Quantidade;
            }
            else
            {
                consumido += -m.Quantidade;
            }
        }

        return new PeriodoFeriasDto
        {
            Id = p.Id,
            UsuarioId = p.UsuarioId,
            UsuarioNome = p.Usuario.Nome,
            InicioAquisitivo = p.InicioAquisitivo,
            FimAquisitivo = p.FimAquisitivo,
            InicioConcessivo = p.InicioConcessivo,
            FimConcessivo = p.FimConcessivo,
            DiasDireito = p.DiasDireito,
            AquisitivoFechado = aquisitivoFechado,
            DireitoAdquirido = direitoAdquirido,
            ProjecaoProporcional = projecao,
            Comprometido = comprometido,
            Consumido = consumido,
            SaldoDisponivel = direitoAdquirido + somaMovimentacoes,
            AquisicaoMaterializada = aquisicaoMaterializada,
            DataCriacao = p.DataCriacao,
            DataAtualizacao = p.DataAtualizacao,
        };
    }
}

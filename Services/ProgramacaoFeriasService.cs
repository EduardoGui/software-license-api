using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class ProgramacaoFeriasService : IProgramacaoFeriasService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly IAuditoriaService _auditoriaService;
    private readonly IPeriodoFeriasService _periodoFeriasService;
    private readonly ILogger<ProgramacaoFeriasService> _logger;

    public ProgramacaoFeriasService(
        AppDbContext context, TimeProvider timeProvider, IAuditoriaService auditoriaService,
        IPeriodoFeriasService periodoFeriasService, ILogger<ProgramacaoFeriasService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _auditoriaService = auditoriaService;
        _periodoFeriasService = periodoFeriasService;
        _logger = logger;
    }

    public async Task<List<ProgramacaoFeriasDto>> GetPendentesAprovacaoAsync()
    {
        var hoje = Hoje();
        var pendentes = await MontarConsultaBase()
            .Where(p => p.Status == ProgramacaoFeriasStatus.Solicitada)
            .OrderBy(p => p.DataSolicitacao)
            .ToListAsync();

        return pendentes.Select(p => ParaDto(p, hoje)).ToList();
    }

    public async Task<List<ProgramacaoFeriasDto>> GetByPeriodoAsync(int periodoFeriasId)
    {
        var hoje = Hoje();
        var programacoes = await MontarConsultaBase()
            .Where(p => p.PeriodoFeriasId == periodoFeriasId)
            .OrderBy(p => p.Sequencia)
            .ToListAsync();

        return programacoes.Select(p => ParaDto(p, hoje)).ToList();
    }

    public async Task<List<ProgramacaoFeriasDto>> GetByUsuarioAsync(int usuarioId)
    {
        var hoje = Hoje();
        var programacoes = await MontarConsultaBase()
            .Where(p => p.PeriodoFerias.UsuarioId == usuarioId)
            .OrderByDescending(p => p.DataInicio)
            .ToListAsync();

        return programacoes.Select(p => ParaDto(p, hoje)).ToList();
    }

    public async Task<ProgramacaoFeriasDto> GetByIdAsync(int id)
    {
        var programacao = await BuscarOuFalhar(id);
        return ParaDto(programacao, Hoje());
    }

    public async Task<ProgramacaoFeriasDto> CreateAsync(int periodoFeriasId, CreateProgramacaoFeriasDto dto, int? usuarioResponsavelId)
    {
        var periodo = await _context.PeriodosFerias
            .Include(p => p.Usuario)
            .Include(p => p.ProgramacoesFerias)
            .FirstOrDefaultAsync(p => p.Id == periodoFeriasId);
        if (periodo is null)
        {
            throw new NotFoundException($"Período de Férias {periodoFeriasId} não encontrado.");
        }

        var politica = await BuscarPoliticaAtivaOuFalhar();
        var hoje = Hoje();
        var dataFim = dto.DataInicio.AddDays(dto.QuantidadeDias - 1);
        var diasAbono = dto.AbonoPecuniario ? dto.DiasAbono : 0;

        await ValidarRegrasDeCriacaoOuEdicaoAsync(periodo, politica, dto, dataFim, diasAbono, programacaoIdAtual: null);

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var programacao = new ProgramacaoFerias
        {
            PeriodoFeriasId = periodoFeriasId,
            Sequencia = periodo.ProgramacoesFerias.Count + 1,
            DataInicio = dto.DataInicio,
            DataFim = dataFim,
            QuantidadeDias = dto.QuantidadeDias,
            Status = ProgramacaoFeriasStatus.Rascunho,
            SolicitanteId = usuarioResponsavelId,
            Observacao = string.IsNullOrWhiteSpace(dto.Observacao) ? null : dto.Observacao.Trim(),
            AdiantamentoDecimoTerceiro = dto.AdiantamentoDecimoTerceiro,
            AbonoPecuniario = dto.AbonoPecuniario,
            DiasAbono = diasAbono,
            DataCriacao = agora,
            DataAtualizacao = agora,
        };

        _context.ProgramacoesFerias.Add(programacao);
        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(
            usuarioResponsavelId, LogAuditoriaEntidade.ProgramacaoFerias, programacao.Id, LogAuditoriaAcao.Criado,
            $"Programação de {dto.QuantidadeDias} dia(s) criada para {periodo.Usuario.Nome} (início {dto.DataInicio:dd/MM/yyyy}).");

        _logger.LogInformation("Programação de Férias {ProgramacaoFeriasId} criada para o período {PeriodoFeriasId}", programacao.Id, periodoFeriasId);

        return ParaDto(await BuscarOuFalhar(programacao.Id), hoje);
    }

    public async Task<ProgramacaoFeriasDto> UpdateAsync(int id, CreateProgramacaoFeriasDto dto, int? usuarioResponsavelId)
    {
        var programacao = await _context.ProgramacoesFerias
            .Include(p => p.PeriodoFerias).ThenInclude(pf => pf.Usuario)
            .Include(p => p.PeriodoFerias).ThenInclude(pf => pf.ProgramacoesFerias)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (programacao is null)
        {
            throw new NotFoundException($"Programação de Férias {id} não encontrada.");
        }

        var podeEditar = programacao.Status == ProgramacaoFeriasStatus.Rascunho || programacao.Status == ProgramacaoFeriasStatus.Aprovada;
        if (!podeEditar)
        {
            throw new BusinessRuleException("Só é possível editar uma programação que esteja em rascunho ou aprovada.");
        }

        var periodo = programacao.PeriodoFerias;
        var politica = await BuscarPoliticaAtivaOuFalhar();
        var hoje = Hoje();
        var estavaAprovada = programacao.Status == ProgramacaoFeriasStatus.Aprovada;

        if (estavaAprovada)
        {
            // Mesma regra de antecedência do Cancelar (art. 137 CLT) - editar uma já aprovada é,
            // na prática, uma remarcação.
            var limite = programacao.DataInicio.AddDays(-politica.DiasAntecedenciaRemarcacao);
            if (hoje > limite)
            {
                throw new BusinessRuleException(
                    $"Uma programação já aprovada só pode ser editada/remarcada até {politica.DiasAntecedenciaRemarcacao} dias antes do início ({limite:dd/MM/yyyy}).");
            }
        }

        var dataFim = dto.DataInicio.AddDays(dto.QuantidadeDias - 1);
        var diasAbono = dto.AbonoPecuniario ? dto.DiasAbono : 0;

        // Enquanto aprovada, o saldo atual já está descontando o débito antigo desta própria
        // programação - soma ele de volta antes de validar, senão a comparação fica injusta (ex.:
        // editar mantendo os mesmos dias pareceria "sem saldo").
        var saldoAAdicionarDeVolta = estavaAprovada ? programacao.QuantidadeDias + programacao.DiasAbono : 0;
        await ValidarRegrasDeCriacaoOuEdicaoAsync(periodo, politica, dto, dataFim, diasAbono, programacaoIdAtual: programacao.Id, saldoAAdicionarDeVolta);

        var agora = _timeProvider.GetUtcNow().UtcDateTime;

        var mudouAlgoComEfeitoNoSaldo = estavaAprovada
            && (programacao.DataInicio != dto.DataInicio || programacao.QuantidadeDias != dto.QuantidadeDias
                || programacao.AbonoPecuniario != dto.AbonoPecuniario || programacao.DiasAbono != diasAbono);

        if (mudouAlgoComEfeitoNoSaldo)
        {
            // Nunca edita/apaga um lançamento já feito (livro-razão) - estorna o débito antigo com
            // lançamentos novos e lança o novo débito, tudo visível no extrato como histórico.
            _context.MovimentacoesSaldoFerias.Add(new MovimentacaoSaldoFerias
            {
                PeriodoFeriasId = periodo.Id,
                Tipo = MovimentacaoSaldoFeriasTipo.ProgramacaoFerias,
                Quantidade = programacao.QuantidadeDias,
                ProgramacaoFeriasId = programacao.Id,
                Data = hoje,
                UsuarioResponsavelId = usuarioResponsavelId,
                Observacao = $"Estorno pela edição (era {programacao.QuantidadeDias} dia(s) a partir de {programacao.DataInicio:dd/MM/yyyy}).",
                DataCriacao = agora,
            });

            if (programacao.AbonoPecuniario && programacao.DiasAbono > 0)
            {
                _context.MovimentacoesSaldoFerias.Add(new MovimentacaoSaldoFerias
                {
                    PeriodoFeriasId = periodo.Id,
                    Tipo = MovimentacaoSaldoFeriasTipo.AbonoPecuniario,
                    Quantidade = programacao.DiasAbono,
                    ProgramacaoFeriasId = programacao.Id,
                    Data = hoje,
                    UsuarioResponsavelId = usuarioResponsavelId,
                    Observacao = "Estorno do abono pela edição.",
                    DataCriacao = agora,
                });
            }

            _context.MovimentacoesSaldoFerias.Add(new MovimentacaoSaldoFerias
            {
                PeriodoFeriasId = periodo.Id,
                Tipo = MovimentacaoSaldoFeriasTipo.ProgramacaoFerias,
                Quantidade = -dto.QuantidadeDias,
                ProgramacaoFeriasId = programacao.Id,
                Data = dto.DataInicio,
                UsuarioResponsavelId = usuarioResponsavelId,
                Observacao = "Novo débito pela edição.",
                DataCriacao = agora,
            });

            if (dto.AbonoPecuniario && diasAbono > 0)
            {
                _context.MovimentacoesSaldoFerias.Add(new MovimentacaoSaldoFerias
                {
                    PeriodoFeriasId = periodo.Id,
                    Tipo = MovimentacaoSaldoFeriasTipo.AbonoPecuniario,
                    Quantidade = -diasAbono,
                    ProgramacaoFeriasId = programacao.Id,
                    Data = dto.DataInicio,
                    UsuarioResponsavelId = usuarioResponsavelId,
                    Observacao = "Novo abono pela edição.",
                    DataCriacao = agora,
                });
            }
        }

        programacao.DataInicio = dto.DataInicio;
        programacao.DataFim = dataFim;
        programacao.QuantidadeDias = dto.QuantidadeDias;
        programacao.Observacao = string.IsNullOrWhiteSpace(dto.Observacao) ? null : dto.Observacao.Trim();
        programacao.AdiantamentoDecimoTerceiro = dto.AdiantamentoDecimoTerceiro;
        programacao.AbonoPecuniario = dto.AbonoPecuniario;
        programacao.DiasAbono = diasAbono;
        programacao.DataAtualizacao = agora;

        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(
            usuarioResponsavelId, LogAuditoriaEntidade.ProgramacaoFerias, id, LogAuditoriaAcao.Atualizado,
            $"Programação editada: {dto.QuantidadeDias} dia(s) a partir de {dto.DataInicio:dd/MM/yyyy}.");

        return ParaDto(await BuscarOuFalhar(id), hoje);
    }

    // Compartilhada por Create/Update - a única diferença entre criar e editar é que a edição
    // exclui a própria programação da contagem de fracionamentos ativos e da checagem de
    // sobreposição (programacaoIdAtual), já que ela mesma está nessa lista, e pode compensar o
    // saldo já debitado por ela mesma quando já estava aprovada (saldoAAdicionarDeVolta).
    private async Task ValidarRegrasDeCriacaoOuEdicaoAsync(
        PeriodoFerias periodo, PoliticaFerias politica, CreateProgramacaoFeriasDto dto, DateOnly dataFim, int diasAbono,
        int? programacaoIdAtual, int saldoAAdicionarDeVolta = 0)
    {
        if (dto.QuantidadeDias < politica.DiasMinimoDemaisFracionamentos)
        {
            throw new BusinessRuleException($"Cada fracionamento deve ter pelo menos {politica.DiasMinimoDemaisFracionamentos} dias.");
        }

        if (dataFim > periodo.FimConcessivo)
        {
            throw new BusinessRuleException($"As férias não podem ultrapassar o fim do período concessivo ({periodo.FimConcessivo:dd/MM/yyyy}).");
        }

        var ativasNoPeriodo = periodo.ProgramacoesFerias.Where(EstaAtiva).Where(p => p.Id != programacaoIdAtual).ToList();

        // Um Recesso Corporativo confirmado também é um "fracionamento" de férias pra efeito da
        // regra de divisão (CLT art. 134 §1º) - conta pra o máximo de fracionamentos e pode, ele
        // sozinho, satisfazer a exigência de "pelo menos um com N dias" (achado do usuário: sem
        // isso, quem já teve 12 dias consumidos por um recesso não conseguia agendar o restante
        // fracionado, porque nenhuma ProgramacaoFerias isolada chegava aos N dias mínimos).
        var recessosDoPeriodo = await _context.RecessosColaborador
            .Where(rc => rc.PeriodoFeriasId == periodo.Id)
            .ToListAsync();

        var totalFracionamentosAtivos = ativasNoPeriodo.Count + recessosDoPeriodo.Count;
        if (totalFracionamentosAtivos + 1 > politica.MaxFracionamentos)
        {
            throw new BusinessRuleException($"Este período já atingiu o máximo de {politica.MaxFracionamentos} fracionamento(s).");
        }

        if (totalFracionamentosAtivos + 1 >= 2)
        {
            var existeFracionamentoMaior = ativasNoPeriodo.Any(p => p.QuantidadeDias >= politica.DiasMinimoUltimoFracionamento)
                || recessosDoPeriodo.Any(rc => rc.DiasAbatidos >= politica.DiasMinimoUltimoFracionamento)
                || dto.QuantidadeDias >= politica.DiasMinimoUltimoFracionamento;
            if (!existeFracionamentoMaior)
            {
                throw new BusinessRuleException(
                    $"Ao dividir em mais de um período, pelo menos um deles deve ter no mínimo {politica.DiasMinimoUltimoFracionamento} dias.");
            }
        }

        await ValidarSemSobreposicao(periodo.UsuarioId, dto.DataInicio, dataFim, programacaoIdAtual);
        await ValidarDataInicioForaDaJanelaDeFeriadoOuFimDeSemana(dto.DataInicio, politica.DiasMinimosAntesFeriadoOuFimDeSemana);

        if (dto.AbonoPecuniario)
        {
            if (!politica.PermiteAbonoPecuniario)
            {
                throw new BusinessRuleException("A política de férias atual não permite abono pecuniário.");
            }

            if (dto.DiasAbono is < 1 || dto.DiasAbono > politica.MaxDiasAbono)
            {
                throw new BusinessRuleException($"Dias de abono deve estar entre 1 e {politica.MaxDiasAbono}.");
            }
        }

        var periodoDto = await _periodoFeriasService.GetByIdAsync(periodo.Id);
        var saldoDisponivelParaComparar = periodoDto.SaldoDisponivel + saldoAAdicionarDeVolta;
        if (dto.QuantidadeDias + diasAbono > saldoDisponivelParaComparar)
        {
            throw new BusinessRuleException($"Saldo disponível insuficiente ({saldoDisponivelParaComparar} dia(s)) para os {dto.QuantidadeDias + diasAbono} dia(s) solicitados.");
        }
    }

    public async Task<ProgramacaoFeriasDto> SolicitarAsync(int id, int? usuarioResponsavelId)
    {
        var programacao = await BuscarOuFalhar(id);

        if (programacao.Status != ProgramacaoFeriasStatus.Rascunho)
        {
            throw new BusinessRuleException("Só é possível solicitar uma programação que esteja em rascunho.");
        }

        programacao.Status = ProgramacaoFeriasStatus.Solicitada;
        programacao.SolicitanteId ??= usuarioResponsavelId;
        programacao.DataSolicitacao = Hoje();
        programacao.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(usuarioResponsavelId, LogAuditoriaEntidade.ProgramacaoFerias, id, LogAuditoriaAcao.Enviado, null);

        return ParaDto(await BuscarOuFalhar(id), Hoje());
    }

    public async Task<ProgramacaoFeriasDto> AprovarAsync(int id, int? usuarioResponsavelId)
    {
        var programacao = await BuscarOuFalhar(id);

        if (programacao.Status != ProgramacaoFeriasStatus.Solicitada)
        {
            throw new BusinessRuleException("Só é possível aprovar uma programação que esteja solicitada.");
        }

        // Revalida o saldo aqui, não na solicitação (decisão do usuário): evita conceder saldo já
        // consumido por outra programação aprovada entre a solicitação e a decisão.
        var periodoDto = await _periodoFeriasService.GetByIdAsync(programacao.PeriodoFeriasId);
        var totalDias = programacao.QuantidadeDias + programacao.DiasAbono;
        if (totalDias > periodoDto.SaldoDisponivel)
        {
            throw new BusinessRuleException($"Saldo disponível insuficiente ({periodoDto.SaldoDisponivel} dia(s)) para aprovar esta programação ({totalDias} dia(s)).");
        }

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        programacao.Status = ProgramacaoFeriasStatus.Aprovada;
        programacao.AprovadorId = usuarioResponsavelId;
        programacao.DataDecisao = agora;
        programacao.DataAtualizacao = agora;

        _context.MovimentacoesSaldoFerias.Add(new MovimentacaoSaldoFerias
        {
            PeriodoFeriasId = programacao.PeriodoFeriasId,
            Tipo = MovimentacaoSaldoFeriasTipo.ProgramacaoFerias,
            Quantidade = -programacao.QuantidadeDias,
            ProgramacaoFeriasId = programacao.Id,
            Data = programacao.DataInicio,
            UsuarioResponsavelId = usuarioResponsavelId,
            Observacao = null,
            DataCriacao = agora,
        });

        if (programacao.AbonoPecuniario && programacao.DiasAbono > 0)
        {
            _context.MovimentacoesSaldoFerias.Add(new MovimentacaoSaldoFerias
            {
                PeriodoFeriasId = programacao.PeriodoFeriasId,
                Tipo = MovimentacaoSaldoFeriasTipo.AbonoPecuniario,
                Quantidade = -programacao.DiasAbono,
                ProgramacaoFeriasId = programacao.Id,
                Data = programacao.DataInicio,
                UsuarioResponsavelId = usuarioResponsavelId,
                Observacao = null,
                DataCriacao = agora,
            });
        }

        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(usuarioResponsavelId, LogAuditoriaEntidade.ProgramacaoFerias, id, LogAuditoriaAcao.Aprovado, null);

        return ParaDto(await BuscarOuFalhar(id), Hoje());
    }

    public async Task<ProgramacaoFeriasDto> ReprovarAsync(int id, DecisaoProgramacaoFeriasDto dto, int? usuarioResponsavelId)
    {
        if (string.IsNullOrWhiteSpace(dto.ObservacaoAprovador))
        {
            throw new BusinessRuleException("Justificativa é obrigatória para reprovar uma programação.");
        }

        var programacao = await BuscarOuFalhar(id);

        if (programacao.Status != ProgramacaoFeriasStatus.Solicitada)
        {
            throw new BusinessRuleException("Só é possível reprovar uma programação que esteja solicitada.");
        }

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        programacao.Status = ProgramacaoFeriasStatus.Reprovada;
        programacao.AprovadorId = usuarioResponsavelId;
        programacao.DataDecisao = agora;
        programacao.ObservacaoAprovador = dto.ObservacaoAprovador.Trim();
        programacao.DataAtualizacao = agora;

        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(usuarioResponsavelId, LogAuditoriaEntidade.ProgramacaoFerias, id, LogAuditoriaAcao.Reprovado, dto.ObservacaoAprovador.Trim());

        return ParaDto(await BuscarOuFalhar(id), Hoje());
    }

    public async Task<ProgramacaoFeriasDto> DevolverAsync(int id, DecisaoProgramacaoFeriasDto dto, int? usuarioResponsavelId)
    {
        if (string.IsNullOrWhiteSpace(dto.ObservacaoAprovador))
        {
            throw new BusinessRuleException("Justificativa é obrigatória para devolver uma programação.");
        }

        var programacao = await BuscarOuFalhar(id);

        if (programacao.Status != ProgramacaoFeriasStatus.Solicitada)
        {
            throw new BusinessRuleException("Só é possível devolver uma programação que esteja solicitada.");
        }

        programacao.Status = ProgramacaoFeriasStatus.Rascunho;
        programacao.ObservacaoAprovador = dto.ObservacaoAprovador.Trim();
        programacao.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(usuarioResponsavelId, LogAuditoriaEntidade.ProgramacaoFerias, id, LogAuditoriaAcao.Devolvido, dto.ObservacaoAprovador.Trim());

        return ParaDto(await BuscarOuFalhar(id), Hoje());
    }

    public async Task<ProgramacaoFeriasDto> CancelarAsync(int id, int? usuarioResponsavelId)
    {
        var programacao = await BuscarOuFalhar(id);
        var politica = await BuscarPoliticaAtivaOuFalhar();

        if (programacao.Status != ProgramacaoFeriasStatus.Rascunho
            && programacao.Status != ProgramacaoFeriasStatus.Solicitada
            && programacao.Status != ProgramacaoFeriasStatus.Aprovada)
        {
            throw new BusinessRuleException("Esta programação não pode mais ser cancelada.");
        }

        if (programacao.Status == ProgramacaoFeriasStatus.Aprovada)
        {
            var hoje = Hoje();
            var limite = programacao.DataInicio.AddDays(-politica.DiasAntecedenciaRemarcacao);
            if (hoje > limite)
            {
                throw new BusinessRuleException(
                    $"Uma programação já aprovada só pode ser cancelada/remarcada até {politica.DiasAntecedenciaRemarcacao} dias antes do início ({limite:dd/MM/yyyy}).");
            }
        }

        programacao.Status = ProgramacaoFeriasStatus.Cancelada;
        programacao.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(usuarioResponsavelId, LogAuditoriaEntidade.ProgramacaoFerias, id, LogAuditoriaAcao.Cancelado, null);

        _logger.LogInformation("Programação de Férias {ProgramacaoFeriasId} cancelada", id);

        return ParaDto(await BuscarOuFalhar(id), Hoje());
    }

    private static bool EstaAtiva(ProgramacaoFerias p) =>
        p.Status != ProgramacaoFeriasStatus.Cancelada && p.Status != ProgramacaoFeriasStatus.Reprovada;

    private async Task ValidarSemSobreposicao(int usuarioId, DateOnly inicio, DateOnly fim, int? programacaoIdAtual)
    {
        var programacoesAtivas = await _context.ProgramacoesFerias
            .Include(p => p.PeriodoFerias)
            .Where(p => p.PeriodoFerias.UsuarioId == usuarioId
                && p.Id != programacaoIdAtual
                && p.Status != ProgramacaoFeriasStatus.Cancelada
                && p.Status != ProgramacaoFeriasStatus.Reprovada)
            .ToListAsync();

        var sobrepoe = programacoesAtivas.Any(p => p.DataInicio <= fim && inicio <= p.DataFim);
        if (sobrepoe)
        {
            throw new BusinessRuleException("Já existe uma programação de férias ativa que se sobrepõe a este intervalo.");
        }
    }

    private async Task ValidarDataInicioForaDaJanelaDeFeriadoOuFimDeSemana(DateOnly dataInicio, int diasMinimos)
    {
        if (diasMinimos <= 0)
        {
            return;
        }

        var dataFinalJanela = dataInicio.AddDays(diasMinimos);
        var feriadosNaJanela = await _context.Feriados
            .Where(f => f.Ativo && f.Data > dataInicio && f.Data <= dataFinalJanela)
            .AnyAsync();

        var caiEmFimDeSemana = false;
        for (var i = 1; i <= diasMinimos; i++)
        {
            var dia = dataInicio.AddDays(i).DayOfWeek;
            if (dia is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                caiEmFimDeSemana = true;
                break;
            }
        }

        if (feriadosNaJanela || caiEmFimDeSemana)
        {
            throw new BusinessRuleException(
                $"A data de início não pode cair a até {diasMinimos} dia(s) de um feriado ou fim de semana (art. 134 §3º CLT).");
        }
    }

    private async Task<PoliticaFerias> BuscarPoliticaAtivaOuFalhar()
    {
        var politica = await _context.PoliticasFerias.FirstOrDefaultAsync(p => p.TipoVinculo == UsuarioTipo.Pj && p.Ativa);
        if (politica is null)
        {
            throw new BusinessRuleException("Nenhuma política de férias ativa configurada para PJ.");
        }

        return politica;
    }

    private IQueryable<ProgramacaoFerias> MontarConsultaBase() =>
        _context.ProgramacoesFerias
            .Include(p => p.PeriodoFerias).ThenInclude(pf => pf.Usuario)
            .Include(p => p.Solicitante)
            .Include(p => p.Aprovador)
            .AsQueryable();

    private async Task<ProgramacaoFerias> BuscarOuFalhar(int id)
    {
        var programacao = await MontarConsultaBase().FirstOrDefaultAsync(p => p.Id == id);
        if (programacao is null)
        {
            throw new NotFoundException($"Programação de Férias {id} não encontrada.");
        }

        return programacao;
    }

    private DateOnly Hoje() => DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

    private static string CalcularStatusEfetivo(ProgramacaoFerias p, DateOnly hoje)
    {
        if (p.Status != ProgramacaoFeriasStatus.Aprovada)
        {
            return p.Status;
        }

        if (hoje > p.DataFim)
        {
            return ProgramacaoFeriasStatus.Concluida;
        }

        if (hoje >= p.DataInicio)
        {
            return ProgramacaoFeriasStatus.EmGozo;
        }

        return ProgramacaoFeriasStatus.Aprovada;
    }

    private static ProgramacaoFeriasDto ParaDto(ProgramacaoFerias p, DateOnly hoje) => new()
    {
        Id = p.Id,
        PeriodoFeriasId = p.PeriodoFeriasId,
        UsuarioId = p.PeriodoFerias.UsuarioId,
        UsuarioNome = p.PeriodoFerias.Usuario.Nome,
        Sequencia = p.Sequencia,
        DataInicio = p.DataInicio,
        DataFim = p.DataFim,
        QuantidadeDias = p.QuantidadeDias,
        Status = p.Status,
        StatusEfetivo = CalcularStatusEfetivo(p, hoje),
        SolicitanteId = p.SolicitanteId,
        SolicitanteNome = p.SolicitanteId is null ? "Administrador" : p.Solicitante?.Nome,
        DataSolicitacao = p.DataSolicitacao,
        AprovadorId = p.AprovadorId,
        AprovadorNome = p.AprovadorId is null && p.DataDecisao is not null ? "Administrador" : p.Aprovador?.Nome,
        DataDecisao = p.DataDecisao,
        ObservacaoAprovador = p.ObservacaoAprovador,
        Observacao = p.Observacao,
        AdiantamentoDecimoTerceiro = p.AdiantamentoDecimoTerceiro,
        AbonoPecuniario = p.AbonoPecuniario,
        DiasAbono = p.DiasAbono,
        DataCriacao = p.DataCriacao,
        DataAtualizacao = p.DataAtualizacao,
    };
}

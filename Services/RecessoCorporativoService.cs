using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class RecessoCorporativoService : IRecessoCorporativoService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly IAuditoriaService _auditoriaService;
    private readonly IPeriodoFeriasService _periodoFeriasService;
    private readonly ILogger<RecessoCorporativoService> _logger;

    public RecessoCorporativoService(
        AppDbContext context, TimeProvider timeProvider, IAuditoriaService auditoriaService,
        IPeriodoFeriasService periodoFeriasService, ILogger<RecessoCorporativoService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _auditoriaService = auditoriaService;
        _periodoFeriasService = periodoFeriasService;
        _logger = logger;
    }

    public async Task<List<RecessoCorporativoDto>> GetAllAsync()
    {
        var recessos = await MontarConsultaBase().OrderByDescending(r => r.DataInicio).ToListAsync();
        return recessos.Select(ParaDto).ToList();
    }

    public async Task<RecessoCorporativoDto> GetByIdAsync(int id)
    {
        var recesso = await BuscarOuFalhar(id);
        return ParaDto(recesso);
    }

    public async Task<RecessoCorporativoDto> CreateAsync(CreateRecessoCorporativoDto dto, int? usuarioResponsavelId)
    {
        if (dto.DataFim < dto.DataInicio)
        {
            throw new BusinessRuleException("A data de fim não pode ser anterior à data de início.");
        }

        var diasCorridos = dto.DataFim.DayNumber - dto.DataInicio.DayNumber + 1;
        var diasADescontar = diasCorridos - await ContarFeriadosNacionaisAsync(dto.DataInicio, dto.DataFim);

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var recesso = new RecessoCorporativo
        {
            Nome = dto.Nome.Trim(),
            DataInicio = dto.DataInicio,
            DataFim = dto.DataFim,
            DiasCorridos = diasCorridos,
            DiasADescontar = Math.Max(diasADescontar, 0),
            Status = RecessoCorporativoStatus.Rascunho,
            DataCriacao = agora,
            DataAtualizacao = agora,
        };

        _context.RecessosCorporativos.Add(recesso);
        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(
            usuarioResponsavelId, LogAuditoriaEntidade.RecessoCorporativo, recesso.Id, LogAuditoriaAcao.Criado,
            $"Recesso \"{recesso.Nome}\" criado ({dto.DataInicio:dd/MM/yyyy} a {dto.DataFim:dd/MM/yyyy}, {recesso.DiasADescontar} dia(s) a descontar sugerido).");

        return ParaDto(await BuscarOuFalhar(recesso.Id));
    }

    public async Task<RecessoCorporativoDto> UpdateAsync(int id, UpdateRecessoCorporativoDto dto, int? usuarioResponsavelId)
    {
        var recesso = await BuscarOuFalhar(id);

        if (recesso.Status != RecessoCorporativoStatus.Rascunho)
        {
            throw new BusinessRuleException("Só é possível editar um recesso que ainda esteja em rascunho.");
        }

        if (dto.DataFim < dto.DataInicio)
        {
            throw new BusinessRuleException("A data de fim não pode ser anterior à data de início.");
        }

        recesso.Nome = dto.Nome.Trim();
        recesso.DataInicio = dto.DataInicio;
        recesso.DataFim = dto.DataFim;
        recesso.DiasCorridos = dto.DataFim.DayNumber - dto.DataInicio.DayNumber + 1;
        recesso.DiasADescontar = dto.DiasADescontar;
        recesso.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(
            usuarioResponsavelId, LogAuditoriaEntidade.RecessoCorporativo, id, LogAuditoriaAcao.Atualizado, null);

        return ParaDto(await BuscarOuFalhar(id));
    }

    public async Task<List<RecessoSimulacaoLinhaDto>> SimularAsync(int id, SimularRecessoDto dto)
    {
        var recesso = await BuscarOuFalhar(id);
        var linhas = new List<RecessoSimulacaoLinhaDto>();

        foreach (var usuarioId in dto.UsuarioIds.Distinct())
        {
            linhas.Add(await CalcularLinhaAsync(recesso, usuarioId));
        }

        return linhas;
    }

    public async Task<RecessoCorporativoDto> ConfirmarAsync(int id, SimularRecessoDto dto, int? usuarioResponsavelId)
    {
        var recesso = await BuscarOuFalhar(id);

        if (recesso.Status != RecessoCorporativoStatus.Rascunho)
        {
            throw new BusinessRuleException("Este recesso já foi confirmado.");
        }

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var incluidos = 0;

        foreach (var usuarioId in dto.UsuarioIds.Distinct())
        {
            // Revalidado na confirmação (não reaproveita o resultado de /simular), mesmo padrão já
            // usado em ProgramacaoFeriasService.AprovarAsync - evita usar um saldo defasado se algo
            // mudou entre simular e confirmar.
            var linha = await CalcularLinhaAsync(recesso, usuarioId);
            if (linha.PeriodoFeriasId is null)
            {
                _logger.LogWarning(
                    "Usuário {UsuarioId} não tem Período de Férias gerado - não incluído no Recesso {RecessoId}", usuarioId, id);
                continue;
            }

            var colaborador = new RecessoColaborador
            {
                RecessoCorporativoId = id,
                UsuarioId = usuarioId,
                PeriodoFeriasId = linha.PeriodoFeriasId.Value,
                SaldoAnterior = linha.SaldoAnterior,
                DiasAbatidos = linha.DiasAbatidos,
                SaldoPosterior = linha.SaldoPosterior,
                Situacao = linha.Situacao,
                DataCriacao = agora,
            };
            _context.RecessosColaborador.Add(colaborador);
            await _context.SaveChangesAsync();

            _context.MovimentacoesSaldoFerias.Add(new MovimentacaoSaldoFerias
            {
                PeriodoFeriasId = linha.PeriodoFeriasId.Value,
                Tipo = MovimentacaoSaldoFeriasTipo.Recesso,
                Quantidade = -linha.DiasAbatidos,
                RecessoColaboradorId = colaborador.Id,
                Data = recesso.DataInicio,
                UsuarioResponsavelId = usuarioResponsavelId,
                Observacao = null,
                DataCriacao = agora,
            });
            incluidos++;
        }

        recesso.Status = RecessoCorporativoStatus.Confirmado;
        recesso.DataAtualizacao = agora;
        await _context.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(
            usuarioResponsavelId, LogAuditoriaEntidade.RecessoCorporativo, id, LogAuditoriaAcao.Confirmado,
            $"Recesso \"{recesso.Nome}\" confirmado para {incluidos} colaborador(es).");

        _logger.LogInformation("Recesso Corporativo {RecessoId} confirmado com {Incluidos} colaborador(es)", id, incluidos);

        return ParaDto(await BuscarOuFalhar(id));
    }

    private async Task<RecessoSimulacaoLinhaDto> CalcularLinhaAsync(RecessoCorporativo recesso, int usuarioId)
    {
        var usuario = await _context.Usuarios.FindAsync(usuarioId);
        if (usuario is null)
        {
            throw new NotFoundException($"Usuário {usuarioId} não encontrado.");
        }

        var periodos = await _periodoFeriasService.GetAllAsync(new PeriodoFeriasFiltroDto { UsuarioId = usuarioId });
        var periodoAtual = periodos.FirstOrDefault();

        var saldoAnterior = periodoAtual?.SaldoDisponivel ?? 0;
        var saldoPosterior = saldoAnterior - recesso.DiasADescontar;

        return new RecessoSimulacaoLinhaDto
        {
            UsuarioId = usuario.Id,
            UsuarioNome = usuario.Nome,
            PeriodoFeriasId = periodoAtual?.Id,
            SaldoAnterior = saldoAnterior,
            DiasAbatidos = recesso.DiasADescontar,
            SaldoPosterior = saldoPosterior,
            Situacao = saldoPosterior < 0 ? RecessoColaboradorSituacao.SaldoInsuficiente : RecessoColaboradorSituacao.Normal,
        };
    }

    private async Task<int> ContarFeriadosNacionaisAsync(DateOnly inicio, DateOnly fim) =>
        await _context.Feriados.CountAsync(f => f.Ativo && f.Abrangencia == FeriadoAbrangencia.Nacional && f.Data >= inicio && f.Data <= fim);

    private IQueryable<RecessoCorporativo> MontarConsultaBase() =>
        _context.RecessosCorporativos
            .Include(r => r.Colaboradores).ThenInclude(c => c.Usuario)
            .AsQueryable();

    private async Task<RecessoCorporativo> BuscarOuFalhar(int id)
    {
        var recesso = await MontarConsultaBase().FirstOrDefaultAsync(r => r.Id == id);
        if (recesso is null)
        {
            throw new NotFoundException($"Recesso Corporativo {id} não encontrado.");
        }

        return recesso;
    }

    private static RecessoCorporativoDto ParaDto(RecessoCorporativo r) => new()
    {
        Id = r.Id,
        Nome = r.Nome,
        DataInicio = r.DataInicio,
        DataFim = r.DataFim,
        DiasCorridos = r.DiasCorridos,
        DiasADescontar = r.DiasADescontar,
        Status = r.Status,
        Colaboradores = r.Colaboradores.Select(c => new RecessoColaboradorDto
        {
            Id = c.Id,
            UsuarioId = c.UsuarioId,
            UsuarioNome = c.Usuario.Nome,
            PeriodoFeriasId = c.PeriodoFeriasId,
            SaldoAnterior = c.SaldoAnterior,
            DiasAbatidos = c.DiasAbatidos,
            SaldoPosterior = c.SaldoPosterior,
            Situacao = c.Situacao,
            DataCriacao = c.DataCriacao,
        }).ToList(),
        DataCriacao = r.DataCriacao,
        DataAtualizacao = r.DataAtualizacao,
    };
}

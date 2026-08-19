using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class LicencaService : ILicencaService
{
    private static readonly HashSet<string> PeriodicidadesValidas = [LicencaPeriodicidade.Mensal, LicencaPeriodicidade.Anual];

    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<LicencaService> _logger;

    public LicencaService(AppDbContext context, TimeProvider timeProvider, ILogger<LicencaService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<List<LicencaDto>> GetAllAsync(LicencaFiltroDto filtro)
    {
        var query = _context.Licencas.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Nome))
        {
            query = query.Where(l => EF.Functions.ILike(l.Nome, $"%{filtro.Nome}%"));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Status))
        {
            var ativa = string.Equals(filtro.Status, LicencaStatus.Ativa, StringComparison.OrdinalIgnoreCase);
            query = query.Where(l => l.Ativa == ativa);
        }

        if (filtro.VencimentoAte is not null)
        {
            query = query.Where(l => l.DataTerminoPrevisto <= filtro.VencimentoAte);
        }

        var licencas = await query.OrderBy(l => l.Nome).ToListAsync();
        var emUsoPorLicenca = await ContarEmUsoPorLicencaAsync(licencas.Select(l => l.Id));
        var valorVigentePorLicenca = await BuscarValorVigentePorLicencaAsync(licencas.Select(l => l.Id));

        return licencas
            .Select(l => ParaDto(l, emUsoPorLicenca.GetValueOrDefault(l.Id), valorVigentePorLicenca.GetValueOrDefault(l.Id)))
            .ToList();
    }

    public async Task<LicencaDto> GetByIdAsync(int id)
    {
        var licenca = await BuscarOuFalhar(id);
        var valorVigente = await BuscarValorVigenteAsync(licenca.Id);
        return ParaDto(licenca, await ContarEmUsoAsync(licenca.Id), valorVigente);
    }

    public async Task<LicencaDto> CreateAsync(CreateLicencaDto dto)
    {
        ValidarDatas(dto.DataInicio, dto.DataTerminoPrevisto);
        var periodicidade = ValidarPeriodicidade(dto.Periodicidade);

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var licenca = new Licenca
        {
            Nome = dto.Nome.Trim(),
            Descricao = dto.Descricao,
            QuantidadeTotal = dto.QuantidadeTotal,
            DataInicio = dto.DataInicio,
            DataTerminoPrevisto = dto.DataTerminoPrevisto,
            DiasAntecedenciaAviso = dto.DiasAntecedenciaAviso,
            Observacao = dto.Observacao,
            Ativa = dto.Ativa,
            DataCriacao = agora,
            DataAtualizacao = agora,
        };

        _context.Licencas.Add(licenca);

        var valorInicial = new LicencaValor
        {
            Licenca = licenca,
            Valor = dto.Valor,
            Periodicidade = periodicidade,
            DataVigenciaInicio = dto.DataInicio,
            DataCriacao = agora,
        };
        _context.LicencaValores.Add(valorInicial);

        await _context.SaveChangesAsync();

        _logger.LogInformation("Licença {LicencaId} criada", licenca.Id);

        return ParaDto(licenca, quantidadeEmUso: 0, valorInicial);
    }

    public async Task<LicencaDto> UpdateAsync(int id, UpdateLicencaDto dto)
    {
        var licenca = await BuscarOuFalhar(id);

        ValidarDatas(dto.DataInicio, dto.DataTerminoPrevisto);

        licenca.Nome = dto.Nome.Trim();
        licenca.Descricao = dto.Descricao;
        licenca.QuantidadeTotal = dto.QuantidadeTotal;
        licenca.DataInicio = dto.DataInicio;
        licenca.DataTerminoPrevisto = dto.DataTerminoPrevisto;
        licenca.DiasAntecedenciaAviso = dto.DiasAntecedenciaAviso;
        licenca.Observacao = dto.Observacao;
        licenca.Ativa = dto.Ativa;
        licenca.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Licença {LicencaId} atualizada", licenca.Id);

        return ParaDto(licenca, await ContarEmUsoAsync(licenca.Id), await BuscarValorVigenteAsync(licenca.Id));
    }

    public async Task<LicencaDto> DesativarAsync(int id)
    {
        var licenca = await BuscarOuFalhar(id);

        licenca.Ativa = false;
        licenca.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Licença {LicencaId} desativada", licenca.Id);

        return ParaDto(licenca, await ContarEmUsoAsync(licenca.Id), await BuscarValorVigenteAsync(licenca.Id));
    }

    private Task<int> ContarEmUsoAsync(int licencaId) =>
        _context.UsuarioLicencas.CountAsync(m => m.LicencaId == licencaId && m.DataFim == null);

    private async Task<Dictionary<int, int>> ContarEmUsoPorLicencaAsync(IEnumerable<int> licencaIds) =>
        await _context.UsuarioLicencas
            .Where(m => m.DataFim == null && licencaIds.Contains(m.LicencaId))
            .GroupBy(m => m.LicencaId)
            .Select(g => new { g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Quantidade);

    private Task<LicencaValor?> BuscarValorVigenteAsync(int licencaId)
    {
        var hoje = Hoje();
        return _context.LicencaValores
            .Where(v => v.LicencaId == licencaId && v.DataVigenciaInicio <= hoje)
            .OrderByDescending(v => v.DataVigenciaInicio)
            .ThenByDescending(v => v.Id)
            .FirstOrDefaultAsync();
    }

    private async Task<Dictionary<int, LicencaValor>> BuscarValorVigentePorLicencaAsync(IEnumerable<int> licencaIds)
    {
        var hoje = Hoje();
        return await _context.LicencaValores
            .Where(v => licencaIds.Contains(v.LicencaId) && v.DataVigenciaInicio <= hoje)
            .GroupBy(v => v.LicencaId)
            .Select(g => g.OrderByDescending(v => v.DataVigenciaInicio).ThenByDescending(v => v.Id).First())
            .ToDictionaryAsync(v => v.LicencaId, v => v);
    }

    public async Task<LicencaDto> AdicionarValorAsync(int id, CreateLicencaValorDto dto)
    {
        var licenca = await BuscarOuFalhar(id);
        var periodicidade = ValidarPeriodicidade(dto.Periodicidade);
        var hoje = Hoje();

        if (dto.DataVigenciaInicio < hoje)
        {
            throw new BusinessRuleException("A data de vigência não pode ser retroativa.");
        }

        var vigenciaAtual = await _context.LicencaValores
            .Where(v => v.LicencaId == id)
            .OrderByDescending(v => v.DataVigenciaInicio)
            .ThenByDescending(v => v.Id)
            .FirstOrDefaultAsync();

        if (vigenciaAtual is not null && dto.DataVigenciaInicio <= vigenciaAtual.DataVigenciaInicio)
        {
            throw new BusinessRuleException("A nova vigência deve começar depois da vigência mais recente já cadastrada.");
        }

        _context.LicencaValores.Add(new LicencaValor
        {
            LicencaId = id,
            Valor = dto.Valor,
            Periodicidade = periodicidade,
            DataVigenciaInicio = dto.DataVigenciaInicio,
            DataCriacao = _timeProvider.GetUtcNow().UtcDateTime,
        });

        await _context.SaveChangesAsync();

        _logger.LogInformation("Novo valor registrado para a licença {LicencaId}, vigente a partir de {DataVigenciaInicio}", id, dto.DataVigenciaInicio);

        return ParaDto(licenca, await ContarEmUsoAsync(licenca.Id), await BuscarValorVigenteAsync(licenca.Id));
    }

    public async Task<List<LicencaValorDto>> ListarValoresAsync(int id)
    {
        await BuscarOuFalhar(id);

        return await _context.LicencaValores
            .Where(v => v.LicencaId == id)
            .OrderByDescending(v => v.DataVigenciaInicio)
            .ThenByDescending(v => v.Id)
            .Select(v => new LicencaValorDto
            {
                Id = v.Id,
                Valor = v.Valor,
                Periodicidade = v.Periodicidade,
                DataVigenciaInicio = v.DataVigenciaInicio,
                DataCriacao = v.DataCriacao,
            })
            .ToListAsync();
    }

    private DateOnly Hoje() => DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

    private async Task<Licenca> BuscarOuFalhar(int id)
    {
        var licenca = await _context.Licencas.FindAsync(id);
        if (licenca is null)
        {
            throw new NotFoundException($"Licença {id} não encontrada.");
        }

        return licenca;
    }

    private static void ValidarDatas(DateOnly dataInicio, DateOnly dataTerminoPrevisto)
    {
        if (dataTerminoPrevisto <= dataInicio)
        {
            throw new BusinessRuleException("A data de término previsto deve ser posterior à data de início.");
        }
    }

    private static string ValidarPeriodicidade(string periodicidade)
    {
        if (!PeriodicidadesValidas.Contains(periodicidade))
        {
            throw new BusinessRuleException("Periodicidade deve ser 'Mensal' ou 'Anual'.");
        }

        return periodicidade;
    }

    private static LicencaDto ParaDto(Licenca l, int quantidadeEmUso, LicencaValor? valorVigente)
    {
        return new LicencaDto
        {
            Id = l.Id,
            Nome = l.Nome,
            Descricao = l.Descricao,
            QuantidadeTotal = l.QuantidadeTotal,
            QuantidadeEmUso = quantidadeEmUso,
            QuantidadeDisponivel = l.QuantidadeTotal - quantidadeEmUso,
            DataInicio = l.DataInicio,
            DataTerminoPrevisto = l.DataTerminoPrevisto,
            DiasAntecedenciaAviso = l.DiasAntecedenciaAviso,
            Observacao = l.Observacao,
            Ativa = l.Ativa,
            Status = LicencaStatus.Calcular(l),
            ValorVigente = valorVigente?.Valor,
            Periodicidade = valorVigente?.Periodicidade,
            DataCriacao = l.DataCriacao,
            DataAtualizacao = l.DataAtualizacao,
        };
    }
}

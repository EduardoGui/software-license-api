using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class FeriadoService : IFeriadoService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FeriadoService> _logger;

    public FeriadoService(AppDbContext context, TimeProvider timeProvider, ILogger<FeriadoService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<List<FeriadoDto>> GetAllAsync(FeriadoFiltroDto filtro)
    {
        var query = _context.Feriados.AsQueryable();

        if (filtro.Ano is not null)
        {
            query = query.Where(f => f.Data.Year == filtro.Ano);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Abrangencia))
        {
            query = query.Where(f => f.Abrangencia == filtro.Abrangencia);
        }

        if (filtro.Ativo is not null)
        {
            query = query.Where(f => f.Ativo == filtro.Ativo);
        }

        var feriados = await query.OrderBy(f => f.Data).ToListAsync();
        return feriados.Select(ParaDto).ToList();
    }

    public async Task<FeriadoDto> GetByIdAsync(int id)
    {
        var feriado = await BuscarOuFalhar(id);
        return ParaDto(feriado);
    }

    public async Task<FeriadoDto> CreateAsync(CreateFeriadoDto dto)
    {
        ValidarAbrangencia(dto.Abrangencia, dto.Uf, dto.Municipio);

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var feriado = new Feriado
        {
            Data = dto.Data,
            Descricao = dto.Descricao.Trim(),
            Abrangencia = dto.Abrangencia,
            Uf = dto.Abrangencia == FeriadoAbrangencia.Estadual ? dto.Uf?.Trim().ToUpperInvariant() : null,
            Municipio = dto.Abrangencia == FeriadoAbrangencia.Municipal ? dto.Municipio?.Trim() : null,
            Ativo = dto.Ativo,
            DataCriacao = agora,
            DataAtualizacao = agora,
        };

        _context.Feriados.Add(feriado);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Feriado {FeriadoId} criado", feriado.Id);

        return ParaDto(feriado);
    }

    public async Task<FeriadoDto> UpdateAsync(int id, UpdateFeriadoDto dto)
    {
        var feriado = await BuscarOuFalhar(id);

        ValidarAbrangencia(dto.Abrangencia, dto.Uf, dto.Municipio);

        feriado.Data = dto.Data;
        feriado.Descricao = dto.Descricao.Trim();
        feriado.Abrangencia = dto.Abrangencia;
        feriado.Uf = dto.Abrangencia == FeriadoAbrangencia.Estadual ? dto.Uf?.Trim().ToUpperInvariant() : null;
        feriado.Municipio = dto.Abrangencia == FeriadoAbrangencia.Municipal ? dto.Municipio?.Trim() : null;
        feriado.Ativo = dto.Ativo;
        feriado.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Feriado {FeriadoId} atualizado", feriado.Id);

        return ParaDto(feriado);
    }

    private static void ValidarAbrangencia(string abrangencia, string? uf, string? municipio)
    {
        if (abrangencia != FeriadoAbrangencia.Nacional && abrangencia != FeriadoAbrangencia.Estadual && abrangencia != FeriadoAbrangencia.Municipal)
        {
            throw new BusinessRuleException("Abrangência deve ser 'Nacional', 'Estadual' ou 'Municipal'.");
        }

        if (abrangencia == FeriadoAbrangencia.Estadual && string.IsNullOrWhiteSpace(uf))
        {
            throw new BusinessRuleException("UF é obrigatória para feriados estaduais.");
        }

        if (abrangencia == FeriadoAbrangencia.Municipal && string.IsNullOrWhiteSpace(municipio))
        {
            throw new BusinessRuleException("Município é obrigatório para feriados municipais.");
        }
    }

    private async Task<Feriado> BuscarOuFalhar(int id)
    {
        var feriado = await _context.Feriados.FindAsync(id);
        if (feriado is null)
        {
            throw new NotFoundException($"Feriado {id} não encontrado.");
        }

        return feriado;
    }

    private static FeriadoDto ParaDto(Feriado f) => new()
    {
        Id = f.Id,
        Data = f.Data,
        Descricao = f.Descricao,
        Abrangencia = f.Abrangencia,
        Uf = f.Uf,
        Municipio = f.Municipio,
        Ativo = f.Ativo,
        DataCriacao = f.DataCriacao,
        DataAtualizacao = f.DataAtualizacao,
    };
}

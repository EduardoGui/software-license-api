using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class SetorService : ISetorService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SetorService> _logger;

    public SetorService(AppDbContext context, TimeProvider timeProvider, ILogger<SetorService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<List<SetorDto>> GetAllAsync(SetorFiltroDto filtro)
    {
        var query = _context.Setores.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Nome))
        {
            query = query.Where(s => EF.Functions.ILike(s.Nome, $"%{filtro.Nome}%"));
        }

        if (filtro.Ativo is not null)
        {
            query = query.Where(s => s.Ativo == filtro.Ativo);
        }

        var setores = await query.OrderBy(s => s.Nome).ToListAsync();
        var aprovadoresPorSetor = await BuscarAprovadoresPorSetorAsync(setores.Select(s => s.Id));

        return setores.Select(s => ParaDto(s, aprovadoresPorSetor.GetValueOrDefault(s.Id, []))).ToList();
    }

    public async Task<SetorDto> GetByIdAsync(int id)
    {
        var setor = await BuscarOuFalhar(id);
        return ParaDto(setor, await BuscarAprovadoresAsync(id));
    }

    public async Task<SetorDto> CreateAsync(CreateSetorDto dto)
    {
        await ValidarNomeUnico(dto.Nome, setorIdAtual: null);

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var setor = new Setor
        {
            Nome = dto.Nome.Trim(),
            Ativo = dto.Ativo,
            DataCriacao = agora,
            DataAtualizacao = agora,
        };

        _context.Setores.Add(setor);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Setor {SetorId} criado", setor.Id);

        return ParaDto(setor, []);
    }

    public async Task<SetorDto> UpdateAsync(int id, UpdateSetorDto dto)
    {
        var setor = await BuscarOuFalhar(id);

        await ValidarNomeUnico(dto.Nome, setorIdAtual: id);

        setor.Nome = dto.Nome.Trim();
        setor.Ativo = dto.Ativo;
        setor.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Setor {SetorId} atualizado", setor.Id);

        return ParaDto(setor, await BuscarAprovadoresAsync(id));
    }

    public async Task<SetorDto> AdicionarAprovadorAsync(int setorId, CreateSetorAprovadorDto dto)
    {
        var setor = await BuscarOuFalhar(setorId);

        var usuario = await _context.Usuarios.FindAsync(dto.UsuarioId);
        if (usuario is null)
        {
            throw new NotFoundException($"Usuário {dto.UsuarioId} não encontrado.");
        }

        var jaEhAprovador = await _context.SetorAprovadores
            .AnyAsync(a => a.SetorId == setorId && a.UsuarioId == dto.UsuarioId);
        if (jaEhAprovador)
        {
            throw new BusinessRuleException("Este usuário já é aprovador deste setor.");
        }

        _context.SetorAprovadores.Add(new SetorAprovador
        {
            SetorId = setorId,
            UsuarioId = dto.UsuarioId,
            DataCriacao = _timeProvider.GetUtcNow().UtcDateTime,
        });
        await _context.SaveChangesAsync();

        _logger.LogInformation("Usuário {UsuarioId} adicionado como aprovador do setor {SetorId}", dto.UsuarioId, setorId);

        return ParaDto(setor, await BuscarAprovadoresAsync(setorId));
    }

    public async Task<SetorDto> RemoverAprovadorAsync(int setorId, int aprovadorId)
    {
        var setor = await BuscarOuFalhar(setorId);

        var aprovador = await _context.SetorAprovadores
            .FirstOrDefaultAsync(a => a.Id == aprovadorId && a.SetorId == setorId);
        if (aprovador is null)
        {
            throw new NotFoundException($"Aprovador {aprovadorId} não encontrado neste setor.");
        }

        _context.SetorAprovadores.Remove(aprovador);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Aprovador {AprovadorId} removido do setor {SetorId}", aprovadorId, setorId);

        return ParaDto(setor, await BuscarAprovadoresAsync(setorId));
    }

    private async Task<Setor> BuscarOuFalhar(int id)
    {
        var setor = await _context.Setores.FindAsync(id);
        if (setor is null)
        {
            throw new NotFoundException($"Setor {id} não encontrado.");
        }

        return setor;
    }

    private async Task ValidarNomeUnico(string nome, int? setorIdAtual)
    {
        var nomeNormalizado = nome.Trim();
        var existe = await _context.Setores
            .AnyAsync(s => s.Nome == nomeNormalizado && s.Id != setorIdAtual);

        if (existe)
        {
            throw new BusinessRuleException("Já existe um setor cadastrado com este nome.");
        }
    }

    private Task<List<SetorAprovadorDto>> BuscarAprovadoresAsync(int setorId) =>
        _context.SetorAprovadores
            .Where(a => a.SetorId == setorId)
            .OrderBy(a => a.Usuario.Nome)
            .Select(a => new SetorAprovadorDto { Id = a.Id, UsuarioId = a.UsuarioId, UsuarioNome = a.Usuario.Nome })
            .ToListAsync();

    private async Task<Dictionary<int, List<SetorAprovadorDto>>> BuscarAprovadoresPorSetorAsync(IEnumerable<int> setorIds)
    {
        var aprovadores = await _context.SetorAprovadores
            .Where(a => setorIds.Contains(a.SetorId))
            .OrderBy(a => a.Usuario.Nome)
            .Select(a => new { a.SetorId, Aprovador = new SetorAprovadorDto { Id = a.Id, UsuarioId = a.UsuarioId, UsuarioNome = a.Usuario.Nome } })
            .ToListAsync();

        return aprovadores
            .GroupBy(a => a.SetorId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.Aprovador).ToList());
    }

    private static SetorDto ParaDto(Setor s, List<SetorAprovadorDto> aprovadores)
    {
        return new SetorDto
        {
            Id = s.Id,
            Nome = s.Nome,
            Ativo = s.Ativo,
            Aprovadores = aprovadores,
            DataCriacao = s.DataCriacao,
            DataAtualizacao = s.DataAtualizacao,
        };
    }
}

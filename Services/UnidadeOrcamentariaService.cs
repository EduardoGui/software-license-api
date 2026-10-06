using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class UnidadeOrcamentariaService : IUnidadeOrcamentariaService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UnidadeOrcamentariaService> _logger;

    public UnidadeOrcamentariaService(AppDbContext context, TimeProvider timeProvider, ILogger<UnidadeOrcamentariaService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<List<UnidadeOrcamentariaDto>> GetAllAsync(UnidadeOrcamentariaFiltroDto filtro)
    {
        var query = MontarConsultaBase();

        if (filtro.SetorId is not null)
        {
            query = query.Where(u => u.SetorId == filtro.SetorId);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Codigo))
        {
            query = query.Where(u => EF.Functions.ILike(u.Codigo, $"%{filtro.Codigo}%"));
        }

        if (filtro.Ativa is not null)
        {
            query = query.Where(u => u.Ativa == filtro.Ativa);
        }

        var unidades = await query.OrderBy(u => u.Setor.Nome).ThenBy(u => u.Codigo).ToListAsync();

        // Descrição: ignora maiúsculas/acentos e aceita * como curinga (ex.: *salário* ou sal*rio). A lista é pequena,
        // então o filtro é feito em memória (o ILIKE do banco é sensível a acento).
        if (!string.IsNullOrWhiteSpace(filtro.Descricao))
        {
            unidades = unidades.Where(u => CorrespondeDescricao(u.Descricao, filtro.Descricao)).ToList();
        }

        return unidades.Select(ParaDto).ToList();
    }

    private static bool CorrespondeDescricao(string descricao, string busca)
    {
        var texto = Normalizar(descricao);
        var posicao = 0;

        foreach (var parte in busca.Split('*', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var indice = texto.IndexOf(Normalizar(parte), posicao, StringComparison.Ordinal);
            if (indice < 0)
            {
                return false;
            }

            posicao = indice + parte.Length;
        }

        return true;
    }

    private static string Normalizar(string valor) => new string(valor
        .Normalize(System.Text.NormalizationForm.FormD)
        .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
        .ToArray()).ToUpperInvariant();

    public async Task<UnidadeOrcamentariaDto> GetByIdAsync(int id)
    {
        var unidade = await BuscarOuFalhar(id);
        return ParaDto(unidade);
    }

    public async Task<UnidadeOrcamentariaDto> CreateAsync(CreateUnidadeOrcamentariaDto dto)
    {
        await ValidarSetorExiste(dto.SetorId);
        await ValidarCodigoUnico(dto.Codigo, unidadeIdAtual: null);

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var unidade = new UnidadeOrcamentaria
        {
            SetorId = dto.SetorId,
            Codigo = dto.Codigo.Trim(),
            Descricao = dto.Descricao.Trim(),
            Apropriacao = string.IsNullOrWhiteSpace(dto.Apropriacao) ? null : dto.Apropriacao.Trim(),
            Ativa = dto.Ativa,
            DataCriacao = agora,
            DataAtualizacao = agora,
        };

        _context.UnidadesOrcamentarias.Add(unidade);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Unidade Orçamentária {UnidadeOrcamentariaId} criada", unidade.Id);

        return ParaDto(await BuscarOuFalhar(unidade.Id));
    }

    public async Task<UnidadeOrcamentariaDto> UpdateAsync(int id, UpdateUnidadeOrcamentariaDto dto)
    {
        var unidade = await BuscarOuFalhar(id);

        await ValidarSetorExiste(dto.SetorId);
        await ValidarCodigoUnico(dto.Codigo, unidadeIdAtual: id);

        unidade.SetorId = dto.SetorId;
        unidade.Codigo = dto.Codigo.Trim();
        unidade.Descricao = dto.Descricao.Trim();
        unidade.Apropriacao = string.IsNullOrWhiteSpace(dto.Apropriacao) ? null : dto.Apropriacao.Trim();
        unidade.Ativa = dto.Ativa;
        unidade.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Unidade Orçamentária {UnidadeOrcamentariaId} atualizada", unidade.Id);

        return ParaDto(await BuscarOuFalhar(id));
    }

    private IQueryable<UnidadeOrcamentaria> MontarConsultaBase() => _context.UnidadesOrcamentarias.Include(u => u.Setor).AsQueryable();

    private async Task<UnidadeOrcamentaria> BuscarOuFalhar(int id)
    {
        var unidade = await MontarConsultaBase().FirstOrDefaultAsync(u => u.Id == id);
        if (unidade is null)
        {
            throw new NotFoundException($"Unidade Orçamentária {id} não encontrada.");
        }

        return unidade;
    }

    private async Task ValidarSetorExiste(int setorId)
    {
        var existe = await _context.Setores.AnyAsync(s => s.Id == setorId);
        if (!existe)
        {
            throw new NotFoundException($"Setor {setorId} não encontrado.");
        }
    }

    private async Task ValidarCodigoUnico(string codigo, int? unidadeIdAtual)
    {
        var codigoNormalizado = codigo.Trim();
        var existe = await _context.UnidadesOrcamentarias
            .AnyAsync(u => u.Codigo == codigoNormalizado && u.Id != unidadeIdAtual);

        if (existe)
        {
            throw new BusinessRuleException("Já existe uma Unidade Orçamentária cadastrada com este código.");
        }
    }

    private static UnidadeOrcamentariaDto ParaDto(UnidadeOrcamentaria u) => new()
    {
        Id = u.Id,
        SetorId = u.SetorId,
        SetorNome = u.Setor.Nome,
        Codigo = u.Codigo,
        Descricao = u.Descricao,
        Apropriacao = u.Apropriacao,
        Ativa = u.Ativa,
        DataCriacao = u.DataCriacao,
        DataAtualizacao = u.DataAtualizacao,
    };
}

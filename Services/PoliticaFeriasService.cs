using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class PoliticaFeriasService : IPoliticaFeriasService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PoliticaFeriasService> _logger;

    public PoliticaFeriasService(AppDbContext context, TimeProvider timeProvider, ILogger<PoliticaFeriasService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<List<PoliticaFeriasDto>> GetAllAsync()
    {
        var politicas = await _context.PoliticasFerias.OrderBy(p => p.Id).ToListAsync();
        return politicas.Select(ParaDto).ToList();
    }

    public async Task<PoliticaFeriasDto> GetByIdAsync(int id)
    {
        var politica = await BuscarOuFalhar(id);
        return ParaDto(politica);
    }

    public async Task<PoliticaFeriasDto> CreateAsync(CreatePoliticaFeriasDto dto)
    {
        ValidarTipoVinculo(dto.TipoVinculo);
        ValidarFracionamento(dto.MaxFracionamentos, dto.DiasMinimoUltimoFracionamento, dto.DiasMinimoDemaisFracionamentos);

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var politica = new PoliticaFerias
        {
            TipoVinculo = dto.TipoVinculo,
            DiasDireitoPorAno = dto.DiasDireitoPorAno,
            MaxFracionamentos = dto.MaxFracionamentos,
            DiasMinimoUltimoFracionamento = dto.DiasMinimoUltimoFracionamento,
            DiasMinimoDemaisFracionamentos = dto.DiasMinimoDemaisFracionamentos,
            DiasAntecedenciaRemarcacao = dto.DiasAntecedenciaRemarcacao,
            DiasAntecedenciaMarcacaoCompulsoria = dto.DiasAntecedenciaMarcacaoCompulsoria,
            PermiteAbonoPecuniario = dto.PermiteAbonoPecuniario,
            MaxDiasAbono = dto.MaxDiasAbono,
            DiasMinimosAntesFeriadoOuFimDeSemana = dto.DiasMinimosAntesFeriadoOuFimDeSemana,
            Ativa = dto.Ativa,
            DataCriacao = agora,
            DataAtualizacao = agora,
        };

        _context.PoliticasFerias.Add(politica);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Política de Férias {PoliticaFeriasId} criada", politica.Id);

        return ParaDto(politica);
    }

    public async Task<PoliticaFeriasDto> UpdateAsync(int id, UpdatePoliticaFeriasDto dto)
    {
        var politica = await BuscarOuFalhar(id);

        ValidarTipoVinculo(dto.TipoVinculo);
        ValidarFracionamento(dto.MaxFracionamentos, dto.DiasMinimoUltimoFracionamento, dto.DiasMinimoDemaisFracionamentos);

        politica.TipoVinculo = dto.TipoVinculo;
        politica.DiasDireitoPorAno = dto.DiasDireitoPorAno;
        politica.MaxFracionamentos = dto.MaxFracionamentos;
        politica.DiasMinimoUltimoFracionamento = dto.DiasMinimoUltimoFracionamento;
        politica.DiasMinimoDemaisFracionamentos = dto.DiasMinimoDemaisFracionamentos;
        politica.DiasAntecedenciaRemarcacao = dto.DiasAntecedenciaRemarcacao;
        politica.DiasAntecedenciaMarcacaoCompulsoria = dto.DiasAntecedenciaMarcacaoCompulsoria;
        politica.PermiteAbonoPecuniario = dto.PermiteAbonoPecuniario;
        politica.MaxDiasAbono = dto.MaxDiasAbono;
        politica.DiasMinimosAntesFeriadoOuFimDeSemana = dto.DiasMinimosAntesFeriadoOuFimDeSemana;
        politica.Ativa = dto.Ativa;
        politica.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Política de Férias {PoliticaFeriasId} atualizada", politica.Id);

        return ParaDto(politica);
    }

    private static void ValidarTipoVinculo(string? tipoVinculo)
    {
        if (tipoVinculo is not null && tipoVinculo != UsuarioTipo.Pj && tipoVinculo != UsuarioTipo.Clt && tipoVinculo != UsuarioTipo.Estagio)
        {
            throw new BusinessRuleException("Tipo de vínculo deve ser 'Pj', 'Clt' ou 'Estagio'.");
        }
    }

    private static void ValidarFracionamento(int maxFracionamentos, int diasMinimoUltimo, int diasMinimoDemais)
    {
        if (diasMinimoUltimo < diasMinimoDemais)
        {
            throw new BusinessRuleException("Os dias mínimos do último fracionamento não podem ser menores que os dias mínimos dos demais fracionamentos.");
        }

        if (maxFracionamentos < 1)
        {
            throw new BusinessRuleException("O máximo de fracionamentos deve ser pelo menos 1.");
        }
    }

    private async Task<PoliticaFerias> BuscarOuFalhar(int id)
    {
        var politica = await _context.PoliticasFerias.FindAsync(id);
        if (politica is null)
        {
            throw new NotFoundException($"Política de Férias {id} não encontrada.");
        }

        return politica;
    }

    private static PoliticaFeriasDto ParaDto(PoliticaFerias p) => new()
    {
        Id = p.Id,
        TipoVinculo = p.TipoVinculo,
        DiasDireitoPorAno = p.DiasDireitoPorAno,
        MaxFracionamentos = p.MaxFracionamentos,
        DiasMinimoUltimoFracionamento = p.DiasMinimoUltimoFracionamento,
        DiasMinimoDemaisFracionamentos = p.DiasMinimoDemaisFracionamentos,
        DiasAntecedenciaRemarcacao = p.DiasAntecedenciaRemarcacao,
        DiasAntecedenciaMarcacaoCompulsoria = p.DiasAntecedenciaMarcacaoCompulsoria,
        PermiteAbonoPecuniario = p.PermiteAbonoPecuniario,
        MaxDiasAbono = p.MaxDiasAbono,
        DiasMinimosAntesFeriadoOuFimDeSemana = p.DiasMinimosAntesFeriadoOuFimDeSemana,
        Ativa = p.Ativa,
        DataCriacao = p.DataCriacao,
        DataAtualizacao = p.DataAtualizacao,
    };
}

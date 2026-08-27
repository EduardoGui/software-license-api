using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class PlanoSaudeCustoService : IPlanoSaudeCustoService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PlanoSaudeCustoService> _logger;

    public PlanoSaudeCustoService(AppDbContext context, TimeProvider timeProvider, ILogger<PlanoSaudeCustoService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<PlanoSaudeMesDto> GetMesAsync(PlanoSaudeMesFiltroDto filtro)
    {
        ValidarAnoMes(filtro.Ano, filtro.Mes);

        var inicioMes = new DateOnly(filtro.Ano, filtro.Mes, 1);
        var fimMes = inicioMes.AddDays(DateTime.DaysInMonth(filtro.Ano, filtro.Mes) - 1);

        var query = _context.Usuarios
            .Include(u => u.EmpresaPj)
            .Where(u => u.DataInicio <= fimMes && (u.DataFim == null || u.DataFim >= inicioMes))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Nome))
        {
            query = query.Where(u => EF.Functions.ILike(u.Nome, $"%{filtro.Nome}%"));
        }

        var usuarios = await query.OrderBy(u => u.Nome).ToListAsync();
        var usuarioIds = usuarios.Select(u => u.Id).ToList();

        var dependentes = await _context.Dependentes
            .Where(d => usuarioIds.Contains(d.UsuarioId) && d.Ativo)
            .ToListAsync();
        var dependentesPorUsuario = dependentes.GroupBy(d => d.UsuarioId).ToDictionary(g => g.Key, g => g.OrderBy(d => d.Nome).ToList());

        var lancamentos = await _context.PlanoSaudeCustos
            .Where(p => usuarioIds.Contains(p.UsuarioId) && p.Ano == filtro.Ano && p.Mes == filtro.Mes)
            .ToListAsync();
        var lancamentoTitular = lancamentos.Where(l => l.DependenteId == null).ToDictionary(l => l.UsuarioId);
        var lancamentoDependente = lancamentos.Where(l => l.DependenteId != null).ToDictionary(l => l.DependenteId!.Value);

        var itensUsuario = usuarios.Select(usuario =>
        {
            var lancamentoUsuario = lancamentoTitular.GetValueOrDefault(usuario.Id);

            return new PlanoSaudeUsuarioMesDto
            {
                UsuarioId = usuario.Id,
                Nome = usuario.Nome,
                Tipo = usuario.Tipo,
                EmpresaPjNome = usuario.EmpresaPj?.RazaoSocial,
                LancamentoId = lancamentoUsuario?.Id,
                ValorMensal = lancamentoUsuario?.ValorMensal,
                ValorCoparticipacao = lancamentoUsuario?.ValorCoparticipacao,
                Dependentes = dependentesPorUsuario.GetValueOrDefault(usuario.Id, [])
                    .Select(dependente =>
                    {
                        var lancamentoDep = lancamentoDependente.GetValueOrDefault(dependente.Id);
                        return new PlanoSaudeDependenteMesDto
                        {
                            DependenteId = dependente.Id,
                            Nome = dependente.Nome,
                            LancamentoId = lancamentoDep?.Id,
                            ValorMensal = lancamentoDep?.ValorMensal,
                            ValorCoparticipacao = lancamentoDep?.ValorCoparticipacao,
                        };
                    })
                    .ToList(),
            };
        }).ToList();

        return new PlanoSaudeMesDto
        {
            Ano = filtro.Ano,
            Mes = filtro.Mes,
            Usuarios = itensUsuario,
        };
    }

    public async Task<PlanoSaudeMesDto> SalvarMesAsync(SalvarPlanoSaudeMesDto dto)
    {
        ValidarAnoMes(dto.Ano, dto.Mes);

        var usuarioIds = dto.Itens.Select(i => i.UsuarioId).Distinct().ToList();
        var usuariosValidos = await _context.Usuarios.Where(u => usuarioIds.Contains(u.Id)).Select(u => u.Id).ToListAsync();

        var dependenteIds = dto.Itens.Where(i => i.DependenteId is not null).Select(i => i.DependenteId!.Value).Distinct().ToList();
        var dependentesValidos = await _context.Dependentes
            .Where(d => dependenteIds.Contains(d.Id))
            .Select(d => new { d.Id, d.UsuarioId })
            .ToListAsync();
        var usuarioPorDependente = dependentesValidos.ToDictionary(d => d.Id, d => d.UsuarioId);

        foreach (var item in dto.Itens)
        {
            if (!usuariosValidos.Contains(item.UsuarioId))
            {
                throw new NotFoundException($"Usuário {item.UsuarioId} não encontrado.");
            }

            if (item.DependenteId is not null)
            {
                if (!usuarioPorDependente.TryGetValue(item.DependenteId.Value, out var usuarioDoDependente))
                {
                    throw new NotFoundException($"Dependente {item.DependenteId} não encontrado.");
                }

                if (usuarioDoDependente != item.UsuarioId)
                {
                    throw new BusinessRuleException($"Dependente {item.DependenteId} não pertence ao usuário {item.UsuarioId}.");
                }
            }
        }

        var existentes = await _context.PlanoSaudeCustos
            .Where(p => usuarioIds.Contains(p.UsuarioId) && p.Ano == dto.Ano && p.Mes == dto.Mes)
            .ToListAsync();

        var agora = _timeProvider.GetUtcNow().UtcDateTime;

        foreach (var item in dto.Itens)
        {
            var existente = existentes.FirstOrDefault(e => e.UsuarioId == item.UsuarioId && e.DependenteId == item.DependenteId);

            if (existente is not null)
            {
                existente.ValorMensal = item.ValorMensal;
                existente.ValorCoparticipacao = item.ValorCoparticipacao;
                existente.DataAtualizacao = agora;
            }
            else
            {
                _context.PlanoSaudeCustos.Add(new PlanoSaudeCusto
                {
                    UsuarioId = item.UsuarioId,
                    DependenteId = item.DependenteId,
                    Ano = dto.Ano,
                    Mes = dto.Mes,
                    ValorMensal = item.ValorMensal,
                    ValorCoparticipacao = item.ValorCoparticipacao,
                    DataCriacao = agora,
                    DataAtualizacao = agora,
                });
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Lançamentos de plano de saúde salvos para {Ano}/{Mes} ({Quantidade} itens)", dto.Ano, dto.Mes, dto.Itens.Count);

        return await GetMesAsync(new PlanoSaudeMesFiltroDto { Ano = dto.Ano, Mes = dto.Mes });
    }

    public async Task RemoverAsync(int id)
    {
        var lancamento = await _context.PlanoSaudeCustos.FindAsync(id);
        if (lancamento is null)
        {
            throw new NotFoundException($"Lançamento {id} não encontrado.");
        }

        _context.PlanoSaudeCustos.Remove(lancamento);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Lançamento de plano de saúde {Id} removido", id);
    }

    private static void ValidarAnoMes(int ano, int mes)
    {
        if (mes < 1 || mes > 12)
        {
            throw new BusinessRuleException("Mês deve estar entre 1 e 12.");
        }

        if (ano < 2000 || ano > 2100)
        {
            throw new BusinessRuleException("Ano inválido.");
        }
    }
}

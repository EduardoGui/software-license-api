using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class EquipamentoService : IEquipamentoService
{
    private static readonly HashSet<string> StatusEditaveis = [EquipamentoStatus.Disponivel, EquipamentoStatus.Manutencao];

    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EquipamentoService> _logger;

    public EquipamentoService(AppDbContext context, TimeProvider timeProvider, ILogger<EquipamentoService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<List<EquipamentoDto>> GetAllAsync(EquipamentoFiltroDto filtro)
    {
        var query = _context.Equipamentos
            .Include(e => e.TipoEquipamento)
            .Include(e => e.NotaFiscalItem)
            .ThenInclude(i => i!.NotaFiscalEntrada)
            .AsQueryable();

        if (filtro.TipoEquipamentoId is not null)
        {
            query = query.Where(e => e.TipoEquipamentoId == filtro.TipoEquipamentoId);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Origem))
        {
            query = query.Where(e => e.Origem == filtro.Origem);
        }

        if (filtro.NotaFiscalEntradaId is not null)
        {
            query = query.Where(e => e.NotaFiscalItem != null && e.NotaFiscalItem.NotaFiscalEntradaId == filtro.NotaFiscalEntradaId);
        }

        var equipamentos = await query.OrderBy(e => e.TipoEquipamento.Nome).ThenBy(e => e.Id).ToListAsync();
        var alocacaoAtivaPorEquipamento = await BuscarAlocacoesAtivasAsync(equipamentos.Select(e => e.Id));

        var dtos = equipamentos.Select(e => ParaDto(e, alocacaoAtivaPorEquipamento.GetValueOrDefault(e.Id))).AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filtro.Status))
        {
            dtos = dtos.Where(d => string.Equals(d.Status, filtro.Status, StringComparison.OrdinalIgnoreCase));
        }

        if (filtro.UsuarioId is not null)
        {
            dtos = dtos.Where(d => d.UsuarioAtualId == filtro.UsuarioId);
        }

        return dtos.ToList();
    }

    public async Task<EquipamentoDto> GetByIdAsync(int id)
    {
        var equipamento = await BuscarOuFalhar(id);
        var alocacaoAtiva = (await BuscarAlocacoesAtivasAsync([id])).GetValueOrDefault(id);
        return ParaDto(equipamento, alocacaoAtiva);
    }

    public async Task<EquipamentoDto> UpdateAsync(int id, UpdateEquipamentoDto dto)
    {
        var equipamento = await BuscarOuFalhar(id);

        var status = dto.Status.Trim();
        if (!StatusEditaveis.Contains(status))
        {
            throw new BusinessRuleException("Status deve ser 'Disponivel' ou 'Manutencao'.");
        }

        equipamento.Marca = dto.Marca;
        equipamento.Modelo = dto.Modelo;
        equipamento.NumeroSerie = dto.NumeroSerie;
        equipamento.Patrimonio = dto.Patrimonio;
        equipamento.FornecedorNome = dto.FornecedorNome;
        equipamento.ValorMensal = equipamento.Origem == EquipamentoOrigem.Locado ? dto.ValorMensal : null;
        equipamento.DataFimContrato = dto.DataFimContrato;
        equipamento.Status = status;
        equipamento.Observacao = dto.Observacao;
        equipamento.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Equipamento {EquipamentoId} atualizado", equipamento.Id);

        var alocacaoAtiva = (await BuscarAlocacoesAtivasAsync([id])).GetValueOrDefault(id);
        return ParaDto(equipamento, alocacaoAtiva);
    }

    public async Task<EquipamentoDto> BaixarAsync(int id)
    {
        var equipamento = await BuscarOuFalhar(id);

        var alocacaoAtiva = (await BuscarAlocacoesAtivasAsync([id])).GetValueOrDefault(id);
        if (alocacaoAtiva is not null)
        {
            throw new BusinessRuleException("Não é possível dar baixa em um equipamento alocado a um usuário. Encerre a alocação antes.");
        }

        var hoje = Hoje();

        equipamento.Status = EquipamentoStatus.Baixado;
        equipamento.DataBaixa = hoje;
        if (equipamento.Origem == EquipamentoOrigem.Locado && equipamento.DataFimContrato is null)
        {
            // Encerra o contrato de locação na baixa, para não continuar sendo cobrado no relatório mensal.
            equipamento.DataFimContrato = hoje;
        }
        equipamento.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Equipamento {EquipamentoId} baixado", equipamento.Id);

        return ParaDto(equipamento, alocacaoAtivaAtual: null);
    }

    private DateOnly Hoje() => DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

    private async Task<Equipamento> BuscarOuFalhar(int id)
    {
        var equipamento = await _context.Equipamentos
            .Include(e => e.TipoEquipamento)
            .Include(e => e.NotaFiscalItem)
            .ThenInclude(i => i!.NotaFiscalEntrada)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (equipamento is null)
        {
            throw new NotFoundException($"Equipamento {id} não encontrado.");
        }

        return equipamento;
    }

    private async Task<Dictionary<int, (int UsuarioId, string UsuarioNome)?>> BuscarAlocacoesAtivasAsync(IEnumerable<int> equipamentoIds)
    {
        var alocacoes = await _context.EquipamentoAlocacoes
            .Where(a => a.DataFim == null && equipamentoIds.Contains(a.EquipamentoId))
            .Include(a => a.Usuario)
            .ToListAsync();

        return alocacoes.ToDictionary(
            a => a.EquipamentoId,
            (int UsuarioId, string UsuarioNome)? (a) => (a.UsuarioId, a.Usuario.Nome));
    }

    private static EquipamentoDto ParaDto(Equipamento e, (int UsuarioId, string UsuarioNome)? alocacaoAtivaAtual)
    {
        var status = e.Status == EquipamentoStatus.Baixado
            ? EquipamentoStatus.Baixado
            : alocacaoAtivaAtual is not null
                ? EquipamentoStatus.EmUso
                : e.Status;

        return new EquipamentoDto
        {
            Id = e.Id,
            TipoEquipamentoId = e.TipoEquipamentoId,
            TipoEquipamentoNome = e.TipoEquipamento.Nome,
            NotaFiscalItemId = e.NotaFiscalItemId,
            NotaFiscalEntradaId = e.NotaFiscalItem?.NotaFiscalEntradaId,
            NumeroNotaFiscal = e.NotaFiscalItem?.NotaFiscalEntrada.Numero,
            Marca = e.Marca,
            Modelo = e.Modelo,
            NumeroSerie = e.NumeroSerie,
            Patrimonio = e.Patrimonio,
            Origem = e.Origem,
            FornecedorNome = e.FornecedorNome,
            ValorMensal = e.ValorMensal,
            DataInicioContrato = e.DataInicioContrato,
            DataFimContrato = e.DataFimContrato,
            Status = status,
            DataBaixa = e.DataBaixa,
            Observacao = e.Observacao,
            UsuarioAtualId = alocacaoAtivaAtual?.UsuarioId,
            UsuarioAtualNome = alocacaoAtivaAtual?.UsuarioNome,
            DataCriacao = e.DataCriacao,
            DataAtualizacao = e.DataAtualizacao,
        };
    }
}

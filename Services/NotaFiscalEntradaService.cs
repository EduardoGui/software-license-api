using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class NotaFiscalEntradaService : INotaFiscalEntradaService
{
    private static readonly HashSet<string> OrigensValidas = [EquipamentoOrigem.Locado, EquipamentoOrigem.Comprado];

    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NotaFiscalEntradaService> _logger;

    public NotaFiscalEntradaService(AppDbContext context, TimeProvider timeProvider, ILogger<NotaFiscalEntradaService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<List<NotaFiscalEntradaDto>> GetAllAsync(NotaFiscalEntradaFiltroDto filtro)
    {
        var query = _context.NotasFiscaisEntrada.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Numero))
        {
            query = query.Where(n => EF.Functions.ILike(n.Numero, $"%{filtro.Numero}%"));
        }

        if (!string.IsNullOrWhiteSpace(filtro.FornecedorNome))
        {
            query = query.Where(n => n.FornecedorNome != null && EF.Functions.ILike(n.FornecedorNome, $"%{filtro.FornecedorNome}%"));
        }

        var notas = await query.OrderByDescending(n => n.DataEntrada).ToListAsync();
        var quantidadeItensPorNota = await ContarItensPorNotaAsync(notas.Select(n => n.Id));

        return notas.Select(n => ParaDto(n, quantidadeItensPorNota.GetValueOrDefault(n.Id))).ToList();
    }

    public async Task<NotaFiscalEntradaDetalheDto> GetByIdAsync(int id)
    {
        var nota = await _context.NotasFiscaisEntrada
            .Include(n => n.Itens)
            .ThenInclude(i => i.TipoEquipamento)
            .FirstOrDefaultAsync(n => n.Id == id);

        if (nota is null)
        {
            throw new NotFoundException($"Nota fiscal de entrada {id} não encontrada.");
        }

        return new NotaFiscalEntradaDetalheDto
        {
            Id = nota.Id,
            Numero = nota.Numero,
            DataEntrada = nota.DataEntrada,
            FornecedorNome = nota.FornecedorNome,
            Observacao = nota.Observacao,
            DataCriacao = nota.DataCriacao,
            DataAtualizacao = nota.DataAtualizacao,
            Itens = nota.Itens.OrderBy(i => i.DataCriacao).Select(ParaItemDto).ToList(),
        };
    }

    public async Task<NotaFiscalEntradaDto> CreateAsync(CreateNotaFiscalEntradaDto dto)
    {
        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var nota = new NotaFiscalEntrada
        {
            Numero = dto.Numero.Trim(),
            DataEntrada = dto.DataEntrada,
            FornecedorNome = dto.FornecedorNome,
            Observacao = dto.Observacao,
            DataCriacao = agora,
            DataAtualizacao = agora,
        };

        _context.NotasFiscaisEntrada.Add(nota);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Nota fiscal de entrada {NotaFiscalEntradaId} criada", nota.Id);

        return ParaDto(nota, quantidadeItens: 0);
    }

    public async Task<NotaFiscalItemDto> AdicionarItemAsync(int notaFiscalEntradaId, CreateNotaFiscalItemDto dto)
    {
        var nota = await _context.NotasFiscaisEntrada.FindAsync(notaFiscalEntradaId);
        if (nota is null)
        {
            throw new NotFoundException($"Nota fiscal de entrada {notaFiscalEntradaId} não encontrada.");
        }

        var tipoEquipamento = await _context.TiposEquipamento.FindAsync(dto.TipoEquipamentoId);
        if (tipoEquipamento is null)
        {
            throw new NotFoundException($"Tipo de equipamento {dto.TipoEquipamentoId} não encontrado.");
        }

        var origem = ValidarOrigem(dto.Origem);

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var item = new NotaFiscalItem
        {
            NotaFiscalEntradaId = nota.Id,
            TipoEquipamentoId = tipoEquipamento.Id,
            Descricao = dto.Descricao,
            Quantidade = dto.Quantidade,
            ValorUnitario = dto.ValorUnitario,
            Origem = origem,
            DataCriacao = agora,
        };

        _context.NotasFiscaisItens.Add(item);
        await _context.SaveChangesAsync();

        var equipamentos = new List<Equipamento>();
        for (var i = 0; i < item.Quantidade; i++)
        {
            equipamentos.Add(new Equipamento
            {
                TipoEquipamentoId = tipoEquipamento.Id,
                NotaFiscalItemId = item.Id,
                Origem = origem,
                FornecedorNome = nota.FornecedorNome,
                ValorMensal = origem == EquipamentoOrigem.Locado ? item.ValorUnitario : null,
                DataInicioContrato = nota.DataEntrada,
                Status = EquipamentoStatus.Disponivel,
                DataCriacao = agora,
                DataAtualizacao = agora,
            });
        }

        _context.Equipamentos.AddRange(equipamentos);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Item {NotaFiscalItemId} adicionado à nota fiscal {NotaFiscalEntradaId}, gerando {Quantidade} equipamento(s) do tipo {TipoEquipamentoId}",
            item.Id, nota.Id, item.Quantidade, tipoEquipamento.Id);

        item.TipoEquipamento = tipoEquipamento;
        return ParaItemDto(item);
    }

    private static string ValidarOrigem(string origem)
    {
        var origemNormalizada = origem.Trim();
        if (!OrigensValidas.Contains(origemNormalizada))
        {
            throw new BusinessRuleException("Origem deve ser 'Locado' ou 'Comprado'.");
        }

        return origemNormalizada;
    }

    private async Task<Dictionary<int, int>> ContarItensPorNotaAsync(IEnumerable<int> notaIds) =>
        await _context.NotasFiscaisItens
            .Where(i => notaIds.Contains(i.NotaFiscalEntradaId))
            .GroupBy(i => i.NotaFiscalEntradaId)
            .Select(g => new { g.Key, Quantidade = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Quantidade);

    private static NotaFiscalEntradaDto ParaDto(NotaFiscalEntrada n, int quantidadeItens)
    {
        return new NotaFiscalEntradaDto
        {
            Id = n.Id,
            Numero = n.Numero,
            DataEntrada = n.DataEntrada,
            FornecedorNome = n.FornecedorNome,
            Observacao = n.Observacao,
            QuantidadeItens = quantidadeItens,
            DataCriacao = n.DataCriacao,
            DataAtualizacao = n.DataAtualizacao,
        };
    }

    private static NotaFiscalItemDto ParaItemDto(NotaFiscalItem i)
    {
        return new NotaFiscalItemDto
        {
            Id = i.Id,
            NotaFiscalEntradaId = i.NotaFiscalEntradaId,
            TipoEquipamentoId = i.TipoEquipamentoId,
            TipoEquipamentoNome = i.TipoEquipamento.Nome,
            Descricao = i.Descricao,
            Quantidade = i.Quantidade,
            ValorUnitario = i.ValorUnitario,
            Origem = i.Origem,
            DataCriacao = i.DataCriacao,
        };
    }
}

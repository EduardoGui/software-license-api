using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class NotaFiscalEntradaService : INotaFiscalEntradaService
{
    private static readonly HashSet<string> OrigensValidas = [EquipamentoOrigem.Locado, EquipamentoOrigem.Comprado];
    private static readonly HashSet<string> DestinosValidos = [NotaFiscalItemDestino.Equipamento, NotaFiscalItemDestino.Patrimonio];

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
        var query = _context.NotasFiscaisEntrada.Include(n => n.Fornecedor).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Numero))
        {
            query = query.Where(n => EF.Functions.ILike(n.Numero, $"%{filtro.Numero}%"));
        }

        if (filtro.FornecedorId is not null)
        {
            query = query.Where(n => n.FornecedorId == filtro.FornecedorId);
        }

        var notas = await query.OrderByDescending(n => n.DataEntrada).ToListAsync();
        var quantidadeItensPorNota = await ContarItensPorNotaAsync(notas.Select(n => n.Id));

        return notas.Select(n => ParaDto(n, quantidadeItensPorNota.GetValueOrDefault(n.Id))).ToList();
    }

    public async Task<NotaFiscalEntradaDetalheDto> GetByIdAsync(int id)
    {
        var nota = await _context.NotasFiscaisEntrada
            .Include(n => n.Fornecedor)
            .Include(n => n.Itens)
            .ThenInclude(i => i.TipoEquipamento)
            .Include(n => n.Itens)
            .ThenInclude(i => i.TipoPatrimonio)
            .Include(n => n.Itens)
            .ThenInclude(i => i.Local)
            .Include(n => n.Itens)
            .ThenInclude(i => i.RateiosUa)
            .ThenInclude(r => r.UnidadeOrcamentaria)
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
            FornecedorNome = nota.Fornecedor?.Nome,
            Observacao = nota.Observacao,
            DataCriacao = nota.DataCriacao,
            DataAtualizacao = nota.DataAtualizacao,
            Itens = nota.Itens.OrderBy(i => i.DataCriacao).Select(ParaItemDto).ToList(),
        };
    }

    public async Task<NotaFiscalEntradaDto> CreateAsync(CreateNotaFiscalEntradaDto dto)
    {
        Fornecedor? fornecedor = null;
        if (dto.FornecedorId is not null)
        {
            fornecedor = await _context.Fornecedores.FindAsync(dto.FornecedorId.Value);
            if (fornecedor is null)
            {
                throw new NotFoundException($"Fornecedor {dto.FornecedorId} não encontrado.");
            }
        }

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var nota = new NotaFiscalEntrada
        {
            Numero = dto.Numero.Trim(),
            DataEntrada = dto.DataEntrada,
            FornecedorId = dto.FornecedorId,
            Observacao = dto.Observacao,
            DataCriacao = agora,
            DataAtualizacao = agora,
        };

        _context.NotasFiscaisEntrada.Add(nota);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Nota fiscal de entrada {NotaFiscalEntradaId} criada", nota.Id);

        nota.Fornecedor = fornecedor;
        return ParaDto(nota, quantidadeItens: 0);
    }

    public async Task<NotaFiscalItemDto> AdicionarItemAsync(int notaFiscalEntradaId, CreateNotaFiscalItemDto dto)
    {
        var nota = await _context.NotasFiscaisEntrada
            .Include(n => n.Fornecedor)
            .FirstOrDefaultAsync(n => n.Id == notaFiscalEntradaId);
        if (nota is null)
        {
            throw new NotFoundException($"Nota fiscal de entrada {notaFiscalEntradaId} não encontrada.");
        }

        var destino = ValidarDestino(dto.Destino);

        return destino switch
        {
            NotaFiscalItemDestino.Patrimonio => await AdicionarItemPatrimonioAsync(nota, dto),
            _ => await AdicionarItemEquipamentoAsync(nota, dto),
        };
    }

    private async Task<NotaFiscalItemDto> AdicionarItemEquipamentoAsync(NotaFiscalEntrada nota, CreateNotaFiscalItemDto dto)
    {
        if (dto.TipoEquipamentoId is null)
        {
            throw new BusinessRuleException("Tipo de equipamento é obrigatório.");
        }

        var tipoEquipamento = await _context.TiposEquipamento.FindAsync(dto.TipoEquipamentoId.Value);
        if (tipoEquipamento is null)
        {
            throw new NotFoundException($"Tipo de equipamento {dto.TipoEquipamentoId} não encontrado.");
        }

        if (string.IsNullOrWhiteSpace(dto.Origem))
        {
            throw new BusinessRuleException("Origem é obrigatória.");
        }

        var origem = ValidarOrigem(dto.Origem);

        await ValidarRateioDoItemAsync(dto);

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var item = new NotaFiscalItem
        {
            NotaFiscalEntradaId = nota.Id,
            Destino = NotaFiscalItemDestino.Equipamento,
            TipoEquipamentoId = tipoEquipamento.Id,
            Descricao = dto.Descricao,
            Quantidade = dto.Quantidade,
            ValorUnitario = dto.ValorUnitario,
            Origem = origem,
            DataCriacao = agora,
            RateiosUa = MontarRateios(dto.RateioUa ?? [], agora),
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
                FornecedorNome = nota.Fornecedor?.Nome,
                // Locado sempre grava um número, nunca null - equipamento sem valor de locação
                // lançado (ex.: reposição/backup) é 0, não "vazio" (achado real: relatório mensal
                // tratava null e 0 de forma diferente, escondendo equipamentos com valor null).
                ValorMensal = origem == EquipamentoOrigem.Locado ? item.ValorUnitario ?? 0m : null,
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

    private async Task<NotaFiscalItemDto> AdicionarItemPatrimonioAsync(NotaFiscalEntrada nota, CreateNotaFiscalItemDto dto)
    {
        if (dto.TipoPatrimonioId is null)
        {
            throw new BusinessRuleException("Tipo de patrimônio é obrigatório.");
        }

        var tipoPatrimonio = await _context.TiposPatrimonio.FindAsync(dto.TipoPatrimonioId.Value);
        if (tipoPatrimonio is null)
        {
            throw new NotFoundException($"Tipo de patrimônio {dto.TipoPatrimonioId} não encontrado.");
        }

        Local? local = null;
        if (dto.LocalId is not null)
        {
            local = await _context.Locais.FindAsync(dto.LocalId.Value);
            if (local is null)
            {
                throw new NotFoundException($"Local {dto.LocalId} não encontrado.");
            }
        }

        await ValidarRateioDoItemAsync(dto);

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var item = new NotaFiscalItem
        {
            NotaFiscalEntradaId = nota.Id,
            Destino = NotaFiscalItemDestino.Patrimonio,
            TipoPatrimonioId = tipoPatrimonio.Id,
            LocalId = dto.LocalId,
            Descricao = dto.Descricao,
            Quantidade = dto.Quantidade,
            ValorUnitario = dto.ValorUnitario,
            Origem = EquipamentoOrigem.Comprado,
            DataCriacao = agora,
            RateiosUa = MontarRateios(dto.RateioUa ?? [], agora),
        };

        _context.NotasFiscaisItens.Add(item);
        await _context.SaveChangesAsync();

        var patrimonioItens = new List<PatrimonioItem>();
        for (var i = 0; i < item.Quantidade; i++)
        {
            patrimonioItens.Add(new PatrimonioItem
            {
                NotaFiscalItemId = item.Id,
                TipoPatrimonioId = tipoPatrimonio.Id,
                Descricao = dto.Descricao,
                LocalId = dto.LocalId,
                Status = PatrimonioItemStatus.Ativo,
                DataCriacao = agora,
                DataAtualizacao = agora,
            });
        }

        _context.PatrimonioItens.AddRange(patrimonioItens);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Item {NotaFiscalItemId} adicionado à nota fiscal {NotaFiscalEntradaId}, gerando {Quantidade} item(ns) de patrimônio do tipo {TipoPatrimonioId}",
            item.Id, nota.Id, item.Quantidade, tipoPatrimonio.Id);

        item.TipoPatrimonio = tipoPatrimonio;
        item.Local = local;
        return ParaItemDto(item);
    }

    public async Task<NotaFiscalItemDto> DefinirRateioUaAsync(int notaFiscalEntradaId, int itemId, DefinirRateioUaDto dto)
    {
        await BuscarNotaOuFalhar(notaFiscalEntradaId);

        var item = await _context.NotasFiscaisItens
            .Include(i => i.RateiosUa)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.NotaFiscalEntradaId == notaFiscalEntradaId)
            ?? throw new BusinessRuleException($"Item {itemId} não pertence a esta nota fiscal.");

        await RateioUaValidador.ValidarPorQuantidadeAsync(_context, dto.Itens, item.Quantidade, "quantidade do item");

        // Só o rateio muda: as unidades (equipamentos/patrimônio) já geradas pelo item não são tocadas.
        _context.NotaFiscalItemRateiosUa.RemoveRange(item.RateiosUa);
        item.RateiosUa = MontarRateios(dto.Itens, _timeProvider.GetUtcNow().UtcDateTime);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Rateio de UA do item {NotaFiscalItemId} da nota fiscal {NotaFiscalEntradaId} definido", itemId, notaFiscalEntradaId);

        var atualizado = await _context.NotasFiscaisItens
            .Include(i => i.TipoEquipamento)
            .Include(i => i.TipoPatrimonio)
            .Include(i => i.Local)
            .Include(i => i.RateiosUa).ThenInclude(r => r.UnidadeOrcamentaria)
            .FirstAsync(i => i.Id == itemId);

        return ParaItemDto(atualizado);
    }

    // A UA é opcional ao adicionar o item (pode ser definida depois); se informada, precisa fechar com a
    // quantidade e é validada antes de qualquer gravação (o item gera unidades).
    private async Task ValidarRateioDoItemAsync(CreateNotaFiscalItemDto dto)
    {
        if (dto.RateioUa is null || dto.RateioUa.Count == 0)
        {
            return;
        }

        await RateioUaValidador.ValidarPorQuantidadeAsync(_context, dto.RateioUa, dto.Quantidade, "quantidade do item");
    }

    private static List<NotaFiscalItemRateioUa> MontarRateios(IEnumerable<ItemRateioUaInputDto> linhas, DateTime agora) =>
        linhas.Select(r => new NotaFiscalItemRateioUa
        {
            UnidadeOrcamentariaId = r.UnidadeOrcamentariaId,
            Quantidade = r.Quantidade,
            DataCriacao = agora,
        }).ToList();

    public async Task<List<AnexoDto>> ListarAnexosAsync(int notaFiscalEntradaId)
    {
        await BuscarNotaOuFalhar(notaFiscalEntradaId);

        return await _context.NotaFiscalEntradaAnexos
            .Where(a => a.NotaFiscalEntradaId == notaFiscalEntradaId)
            .OrderByDescending(a => a.DataUpload)
            .Select(a => new AnexoDto
            {
                Id = a.Id,
                NomeArquivo = a.NomeArquivo,
                TipoConteudo = a.TipoConteudo,
                Tamanho = a.Tamanho,
                DataUpload = a.DataUpload,
            })
            .ToListAsync();
    }

    public async Task<AnexoDto> AdicionarAnexoAsync(int notaFiscalEntradaId, AdicionarAnexoDto dto)
    {
        await BuscarNotaOuFalhar(notaFiscalEntradaId);
        AnexoValidator.Validar(dto.TipoConteudo, dto.Conteudo.Length);

        var anexo = new NotaFiscalEntradaAnexo
        {
            NotaFiscalEntradaId = notaFiscalEntradaId,
            NomeArquivo = dto.NomeArquivo,
            TipoConteudo = dto.TipoConteudo,
            Tamanho = dto.Conteudo.Length,
            Conteudo = dto.Conteudo,
            DataUpload = _timeProvider.GetUtcNow().UtcDateTime,
        };

        _context.NotaFiscalEntradaAnexos.Add(anexo);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Anexo {AnexoId} adicionado à nota fiscal {NotaFiscalEntradaId}", anexo.Id, notaFiscalEntradaId);

        return new AnexoDto
        {
            Id = anexo.Id,
            NomeArquivo = anexo.NomeArquivo,
            TipoConteudo = anexo.TipoConteudo,
            Tamanho = anexo.Tamanho,
            DataUpload = anexo.DataUpload,
        };
    }

    public async Task<AnexoArquivoDto> ObterAnexoAsync(int notaFiscalEntradaId, int anexoId)
    {
        var anexo = await _context.NotaFiscalEntradaAnexos
            .FirstOrDefaultAsync(a => a.Id == anexoId && a.NotaFiscalEntradaId == notaFiscalEntradaId)
            ?? throw new NotFoundException($"Anexo {anexoId} não encontrado.");

        return new AnexoArquivoDto
        {
            NomeArquivo = anexo.NomeArquivo,
            TipoConteudo = anexo.TipoConteudo,
            Conteudo = anexo.Conteudo,
        };
    }

    public async Task ExcluirAnexoAsync(int notaFiscalEntradaId, int anexoId)
    {
        var anexo = await _context.NotaFiscalEntradaAnexos
            .FirstOrDefaultAsync(a => a.Id == anexoId && a.NotaFiscalEntradaId == notaFiscalEntradaId)
            ?? throw new NotFoundException($"Anexo {anexoId} não encontrado.");

        _context.NotaFiscalEntradaAnexos.Remove(anexo);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Anexo {AnexoId} excluído da nota fiscal {NotaFiscalEntradaId}", anexoId, notaFiscalEntradaId);
    }

    private async Task<NotaFiscalEntrada> BuscarNotaOuFalhar(int id)
    {
        var nota = await _context.NotasFiscaisEntrada.FindAsync(id);
        if (nota is null)
        {
            throw new NotFoundException($"Nota fiscal de entrada {id} não encontrada.");
        }

        return nota;
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

    private static string ValidarDestino(string? destino)
    {
        var destinoNormalizado = string.IsNullOrWhiteSpace(destino) ? NotaFiscalItemDestino.Equipamento : destino.Trim();
        if (!DestinosValidos.Contains(destinoNormalizado))
        {
            throw new BusinessRuleException("Destino deve ser 'Equipamento' ou 'Patrimonio'.");
        }

        return destinoNormalizado;
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
            FornecedorNome = n.Fornecedor?.Nome,
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
            Destino = i.Destino,
            TipoEquipamentoId = i.TipoEquipamentoId,
            TipoEquipamentoNome = i.TipoEquipamento?.Nome,
            TipoPatrimonioId = i.TipoPatrimonioId,
            TipoPatrimonioNome = i.TipoPatrimonio?.Nome,
            LocalId = i.LocalId,
            LocalNome = i.Local?.Nome,
            Descricao = i.Descricao,
            Quantidade = i.Quantidade,
            ValorUnitario = i.ValorUnitario,
            Origem = i.Origem,
            DataCriacao = i.DataCriacao,
            RateioUa = i.RateiosUa.Select(r => new NotaFiscalItemRateioUaDto
            {
                UnidadeOrcamentariaId = r.UnidadeOrcamentariaId,
                UnidadeOrcamentariaCodigo = r.UnidadeOrcamentaria?.Codigo ?? string.Empty,
                UnidadeOrcamentariaDescricao = r.UnidadeOrcamentaria?.Descricao ?? string.Empty,
                Quantidade = r.Quantidade,
            }).ToList(),
        };
    }
}

using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class ContratoService : IContratoService
{
    private static readonly HashSet<string> TiposMedicaoValidos =
    [
        TipoMedicao.MensalFixo, TipoMedicao.MensalVariavel, TipoMedicao.QuantidadeXPrecoUnitario,
        TipoMedicao.EtapasPercentuais, TipoMedicao.ParcelaUnica, TipoMedicao.Outro,
    ];

    private static readonly HashSet<string> MetodosProRataValidos =
    [
        MetodoProRata.DiasCorridos, MetodoProRata.MesComercial30, MetodoProRata.DiasUteis,
        MetodoProRata.FracaoManual, MetodoProRata.ValorManual,
    ];

    private static readonly HashSet<string> StatusValidos = [ContratoStatus.Ativo, ContratoStatus.Encerrado, ContratoStatus.Suspenso];

    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ContratoService> _logger;

    public ContratoService(AppDbContext context, TimeProvider timeProvider, ILogger<ContratoService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<List<ContratoDto>> GetAllAsync(ContratoFiltroDto filtro)
    {
        var query = _context.Contratos.Include(c => c.Fornecedor).Include(c => c.Itens).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Numero))
        {
            query = query.Where(c => EF.Functions.ILike(c.Numero, $"%{filtro.Numero}%"));
        }

        if (filtro.FornecedorId is not null)
        {
            query = query.Where(c => c.FornecedorId == filtro.FornecedorId);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Status))
        {
            query = query.Where(c => c.Status == filtro.Status);
        }

        var contratos = await query.OrderByDescending(c => c.DataAssinatura).ToListAsync();
        var aditivosPorContrato = await ObterAditivosFormalizadosPorContratoAsync(contratos.Select(c => c.Id));

        if (filtro.VigenciaFimAte is not null)
        {
            contratos = contratos
                .Where(c => CalcularVigenciaFimAtual(c, aditivosPorContrato.GetValueOrDefault(c.Id)) <= filtro.VigenciaFimAte)
                .ToList();
        }

        return contratos.Select(c => ParaDto(c, aditivosPorContrato.GetValueOrDefault(c.Id))).ToList();
    }

    public async Task<ContratoDetalheDto> GetByIdAsync(int id)
    {
        var contrato = await BuscarComItensOuFalhar(id);
        var medicaoConfig = await _context.ContratoMedicaoConfigs.FirstOrDefaultAsync(m => m.ContratoId == id);
        var faturamentoConfig = await _context.ContratoFaturamentoConfigs.FirstOrDefaultAsync(f => f.ContratoId == id);
        var aditivosFormalizados = await _context.Aditivos
            .Where(a => a.ContratoId == id && a.Status == AditivoStatus.Formalizado)
            .ToListAsync();

        return new ContratoDetalheDto
        {
            Id = contrato.Id,
            Numero = contrato.Numero,
            FornecedorId = contrato.FornecedorId,
            FornecedorNome = contrato.Fornecedor.Nome,
            Objeto = contrato.Objeto,
            Natureza = contrato.Natureza,
            DataAssinatura = contrato.DataAssinatura,
            DataInicioVigencia = contrato.DataInicioVigencia,
            DataFimVigenciaOriginal = contrato.DataFimVigenciaOriginal,
            DataFimVigenciaAtual = CalcularVigenciaFimAtual(contrato, aditivosFormalizados),
            ValorOriginal = contrato.ValorOriginal,
            ValorAtual = CalcularValorAtual(contrato, aditivosFormalizados),
            Status = contrato.Status,
            Observacoes = contrato.Observacoes,
            DataCriacao = contrato.DataCriacao,
            DataAtualizacao = contrato.DataAtualizacao,
            Itens = contrato.Itens.Select(ParaItemDto).ToList(),
            MedicaoConfig = medicaoConfig is null ? null : ParaMedicaoConfigDto(medicaoConfig),
            FaturamentoConfig = faturamentoConfig is null ? null : ParaFaturamentoConfigDto(faturamentoConfig),
        };
    }

    public async Task<ContratoDto> CreateAsync(CreateContratoDto dto)
    {
        var fornecedor = await _context.Fornecedores.FindAsync(dto.FornecedorId)
            ?? throw new NotFoundException($"Fornecedor {dto.FornecedorId} não encontrado.");

        if (await _context.Contratos.AnyAsync(c => c.Numero == dto.Numero.Trim()))
        {
            throw new BusinessRuleException("Já existe um contrato cadastrado com este número.");
        }

        if (dto.DataFimVigenciaOriginal <= dto.DataInicioVigencia)
        {
            throw new BusinessRuleException("Fim de vigência deve ser posterior ao início de vigência.");
        }

        if (dto.Itens.Count == 0)
        {
            throw new BusinessRuleException("Contrato deve ter ao menos um item.");
        }

        ValidarMedicaoConfig(dto.MedicaoConfig.TipoMedicao, dto.MedicaoConfig.MetodoProRata);

        if (dto.FaturamentoConfig.DiaFinalJanelaNf < dto.FaturamentoConfig.DiaInicialJanelaNf)
        {
            throw new BusinessRuleException("Dia final da janela de NF deve ser maior ou igual ao dia inicial.");
        }

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var contrato = new Contrato
        {
            Numero = dto.Numero.Trim(),
            FornecedorId = dto.FornecedorId,
            Objeto = dto.Objeto.Trim(),
            Natureza = dto.Natureza?.Trim(),
            DataAssinatura = dto.DataAssinatura,
            DataInicioVigencia = dto.DataInicioVigencia,
            DataFimVigenciaOriginal = dto.DataFimVigenciaOriginal,
            ValorOriginal = dto.ValorOriginal,
            Status = ContratoStatus.Ativo,
            Observacoes = dto.Observacoes,
            DataCriacao = agora,
            DataAtualizacao = agora,
            Itens = dto.Itens.Select(i => new ContratoItem
            {
                Codigo = i.Codigo?.Trim(),
                Descricao = i.Descricao.Trim(),
                Unidade = i.Unidade.Trim(),
                QuantidadeContratada = i.QuantidadeContratada,
                ValorUnitario = i.ValorUnitario,
                DataCriacao = agora,
                DataAtualizacao = agora,
            }).ToList(),
        };

        _context.Contratos.Add(contrato);
        await _context.SaveChangesAsync();

        _context.ContratoMedicaoConfigs.Add(new ContratoMedicaoConfig
        {
            ContratoId = contrato.Id,
            TipoMedicao = dto.MedicaoConfig.TipoMedicao,
            DiaInicioPeriodo = dto.MedicaoConfig.DiaInicioPeriodo,
            DiaFimPeriodo = dto.MedicaoConfig.DiaFimPeriodo,
            ExigeBm = dto.MedicaoConfig.ExigeBm,
            ExigeAprovacao = dto.MedicaoConfig.ExigeAprovacao,
            ExigeAssinatura = dto.MedicaoConfig.ExigeAssinatura,
            PermiteProRata = dto.MedicaoConfig.PermiteProRata,
            MetodoProRata = dto.MedicaoConfig.MetodoProRata,
            DiasAntecedenciaAlerta = dto.MedicaoConfig.DiasAntecedenciaAlerta,
        });

        _context.ContratoFaturamentoConfigs.Add(new ContratoFaturamentoConfig
        {
            ContratoId = contrato.Id,
            DiaInicialJanelaNf = dto.FaturamentoConfig.DiaInicialJanelaNf,
            DiaFinalJanelaNf = dto.FaturamentoConfig.DiaFinalJanelaNf,
            ExigeBmAprovado = dto.FaturamentoConfig.ExigeBmAprovado,
            ExigeBmAssinado = dto.FaturamentoConfig.ExigeBmAssinado,
            PrazoPagamentoDias = dto.FaturamentoConfig.PrazoPagamentoDias,
        });

        await _context.SaveChangesAsync();

        _logger.LogInformation("Contrato {ContratoId} criado", contrato.Id);

        contrato.Fornecedor = fornecedor;
        return ParaDto(contrato, null);
    }

    public async Task<ContratoDto> UpdateAsync(int id, UpdateContratoDto dto)
    {
        var contrato = await BuscarComItensOuFalhar(id);

        if (!StatusValidos.Contains(dto.Status))
        {
            throw new BusinessRuleException("Status deve ser 'Ativo', 'Encerrado' ou 'Suspenso'.");
        }

        contrato.Objeto = dto.Objeto.Trim();
        contrato.Natureza = dto.Natureza?.Trim();
        contrato.Status = dto.Status;
        contrato.Observacoes = dto.Observacoes;
        contrato.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Contrato {ContratoId} atualizado", contrato.Id);

        var aditivosFormalizados = await _context.Aditivos
            .Where(a => a.ContratoId == contrato.Id && a.Status == AditivoStatus.Formalizado)
            .ToListAsync();

        return ParaDto(contrato, aditivosFormalizados);
    }

    public async Task<ContratoMedicaoConfigDto> AtualizarMedicaoConfigAsync(int id, UpdateContratoMedicaoConfigDto dto)
    {
        await BuscarOuFalhar(id);
        var config = await _context.ContratoMedicaoConfigs.FirstOrDefaultAsync(m => m.ContratoId == id)
            ?? throw new NotFoundException($"Configuração de medição do contrato {id} não encontrada.");

        ValidarMedicaoConfig(dto.TipoMedicao, dto.MetodoProRata);

        config.TipoMedicao = dto.TipoMedicao;
        config.DiaInicioPeriodo = dto.DiaInicioPeriodo;
        config.DiaFimPeriodo = dto.DiaFimPeriodo;
        config.ExigeBm = dto.ExigeBm;
        config.ExigeAprovacao = dto.ExigeAprovacao;
        config.ExigeAssinatura = dto.ExigeAssinatura;
        config.PermiteProRata = dto.PermiteProRata;
        config.MetodoProRata = dto.MetodoProRata;
        config.DiasAntecedenciaAlerta = dto.DiasAntecedenciaAlerta;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Configuração de medição do contrato {ContratoId} atualizada", id);

        return ParaMedicaoConfigDto(config);
    }

    public async Task<ContratoFaturamentoConfigDto> AtualizarFaturamentoConfigAsync(int id, UpdateContratoFaturamentoConfigDto dto)
    {
        await BuscarOuFalhar(id);
        var config = await _context.ContratoFaturamentoConfigs.FirstOrDefaultAsync(f => f.ContratoId == id)
            ?? throw new NotFoundException($"Configuração de faturamento do contrato {id} não encontrada.");

        if (dto.DiaFinalJanelaNf < dto.DiaInicialJanelaNf)
        {
            throw new BusinessRuleException("Dia final da janela de NF deve ser maior ou igual ao dia inicial.");
        }

        config.DiaInicialJanelaNf = dto.DiaInicialJanelaNf;
        config.DiaFinalJanelaNf = dto.DiaFinalJanelaNf;
        config.ExigeBmAprovado = dto.ExigeBmAprovado;
        config.ExigeBmAssinado = dto.ExigeBmAssinado;
        config.PrazoPagamentoDias = dto.PrazoPagamentoDias;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Configuração de faturamento do contrato {ContratoId} atualizada", id);

        return ParaFaturamentoConfigDto(config);
    }

    public async Task<List<AnexoDto>> ListarAnexosAsync(int contratoId)
    {
        await BuscarOuFalhar(contratoId);

        return await _context.ContratoAnexos
            .Where(a => a.ContratoId == contratoId)
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

    public async Task<AnexoDto> AdicionarAnexoAsync(int contratoId, AdicionarAnexoDto dto)
    {
        await BuscarOuFalhar(contratoId);
        AnexoValidator.Validar(dto.TipoConteudo, dto.Conteudo.Length);

        var anexo = new ContratoAnexo
        {
            ContratoId = contratoId,
            NomeArquivo = dto.NomeArquivo,
            TipoConteudo = dto.TipoConteudo,
            Tamanho = dto.Conteudo.Length,
            Conteudo = dto.Conteudo,
            DataUpload = _timeProvider.GetUtcNow().UtcDateTime,
        };

        _context.ContratoAnexos.Add(anexo);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Anexo {AnexoId} adicionado ao contrato {ContratoId}", anexo.Id, contratoId);

        return new AnexoDto
        {
            Id = anexo.Id,
            NomeArquivo = anexo.NomeArquivo,
            TipoConteudo = anexo.TipoConteudo,
            Tamanho = anexo.Tamanho,
            DataUpload = anexo.DataUpload,
        };
    }

    public async Task<AnexoArquivoDto> ObterAnexoAsync(int contratoId, int anexoId)
    {
        var anexo = await _context.ContratoAnexos
            .FirstOrDefaultAsync(a => a.Id == anexoId && a.ContratoId == contratoId)
            ?? throw new NotFoundException($"Anexo {anexoId} não encontrado.");

        return new AnexoArquivoDto
        {
            NomeArquivo = anexo.NomeArquivo,
            TipoConteudo = anexo.TipoConteudo,
            Conteudo = anexo.Conteudo,
        };
    }

    public async Task ExcluirAnexoAsync(int contratoId, int anexoId)
    {
        var anexo = await _context.ContratoAnexos
            .FirstOrDefaultAsync(a => a.Id == anexoId && a.ContratoId == contratoId)
            ?? throw new NotFoundException($"Anexo {anexoId} não encontrado.");

        _context.ContratoAnexos.Remove(anexo);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Anexo {AnexoId} excluído do contrato {ContratoId}", anexoId, contratoId);
    }

    public async Task<List<AditivoDto>> ListarAditivosAsync(int contratoId)
    {
        await BuscarOuFalhar(contratoId);

        var aditivos = await _context.Aditivos
            .Include(a => a.Itens).ThenInclude(i => i.ContratoItem)
            .Where(a => a.ContratoId == contratoId)
            .OrderBy(a => a.Numero)
            .ToListAsync();

        return aditivos.Select(ParaAditivoDto).ToList();
    }

    public async Task<AditivoDto> CriarAditivoAsync(int contratoId, CreateAditivoDto dto)
    {
        var contrato = await BuscarOuFalhar(contratoId);

        if (dto.NovaDataFimVigencia is not null && dto.NovaDataFimVigencia <= contrato.DataInicioVigencia)
        {
            throw new BusinessRuleException("Nova data de fim de vigência deve ser posterior ao início de vigência do contrato.");
        }

        var itensValidados = new List<AditivoItem>();
        foreach (var itemDto in dto.Itens)
        {
            ContratoItem? contratoItem = null;
            if (itemDto.ContratoItemId is not null)
            {
                contratoItem = await _context.ContratoItens.FirstOrDefaultAsync(i => i.Id == itemDto.ContratoItemId && i.ContratoId == contratoId);
                if (contratoItem is null)
                {
                    throw new BusinessRuleException($"Item {itemDto.ContratoItemId} não pertence a este contrato.");
                }
            }
            else if (string.IsNullOrWhiteSpace(itemDto.DescricaoNovoItem) || string.IsNullOrWhiteSpace(itemDto.UnidadeNovoItem) || itemDto.NovoValorUnitario is null)
            {
                throw new BusinessRuleException("Um item novo do aditivo precisa de descrição, unidade e valor unitário.");
            }

            itensValidados.Add(new AditivoItem
            {
                ContratoItem = contratoItem,
                DescricaoNovoItem = itemDto.DescricaoNovoItem?.Trim(),
                CodigoNovoItem = itemDto.CodigoNovoItem?.Trim(),
                UnidadeNovoItem = itemDto.UnidadeNovoItem?.Trim(),
                DeltaQuantidade = itemDto.DeltaQuantidade,
                NovoValorUnitario = itemDto.NovoValorUnitario,
            });
        }

        var proximoNumero = 1 + await _context.Aditivos
            .Where(a => a.ContratoId == contratoId)
            .Select(a => (int?)a.Numero)
            .MaxAsync() ?? 1;

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var aditivo = new Aditivo
        {
            ContratoId = contratoId,
            Numero = proximoNumero,
            Descricao = dto.Descricao.Trim(),
            DataAssinatura = dto.DataAssinatura,
            DataEfeito = dto.DataEfeito,
            DeltaValor = dto.DeltaValor,
            NovaDataFimVigencia = dto.NovaDataFimVigencia,
            PercentualReajuste = dto.PercentualReajuste,
            Status = AditivoStatus.Previsto,
            Observacao = dto.Observacao,
            DataCriacao = agora,
            DataAtualizacao = agora,
            Itens = itensValidados,
        };

        _context.Aditivos.Add(aditivo);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Aditivo {AditivoId} (nº {Numero}) criado para o contrato {ContratoId}", aditivo.Id, aditivo.Numero, contratoId);

        return ParaAditivoDto(aditivo);
    }

    public async Task<AditivoDto> FormalizarAditivoAsync(int contratoId, int aditivoId)
    {
        await BuscarOuFalhar(contratoId);

        var aditivo = await _context.Aditivos
            .Include(a => a.Itens).ThenInclude(i => i.ContratoItem)
            .FirstOrDefaultAsync(a => a.Id == aditivoId && a.ContratoId == contratoId)
            ?? throw new NotFoundException($"Aditivo {aditivoId} não encontrado.");

        if (aditivo.Status == AditivoStatus.Formalizado)
        {
            throw new BusinessRuleException("Este aditivo já está formalizado.");
        }

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        aditivo.Status = AditivoStatus.Formalizado;
        aditivo.DataFormalizacao = agora;
        aditivo.DataAtualizacao = agora;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Aditivo {AditivoId} do contrato {ContratoId} formalizado", aditivoId, contratoId);

        return ParaAditivoDto(aditivo);
    }

    private static void ValidarMedicaoConfig(string tipoMedicao, string? metodoProRata)
    {
        if (!TiposMedicaoValidos.Contains(tipoMedicao))
        {
            throw new BusinessRuleException("Tipo de medição inválido.");
        }

        if (metodoProRata is not null && !MetodosProRataValidos.Contains(metodoProRata))
        {
            throw new BusinessRuleException("Método de pró-rata inválido.");
        }
    }

    // Só os aditivos com Status = Formalizado entram nesta soma — "Previsto" nunca altera
    // oficialmente valor/vigência (só quando formalizado passa a valer).
    private static decimal CalcularValorAtual(Contrato contrato, List<Aditivo>? aditivosFormalizados)
    {
        if (aditivosFormalizados is null || aditivosFormalizados.Count == 0)
        {
            return contrato.ValorOriginal;
        }

        var delta = aditivosFormalizados.Sum(a =>
            (a.DeltaValor ?? 0) + (a.PercentualReajuste is null ? 0 : contrato.ValorOriginal * a.PercentualReajuste.Value / 100));

        return Math.Round(contrato.ValorOriginal + delta, 2);
    }

    private static DateOnly CalcularVigenciaFimAtual(Contrato contrato, List<Aditivo>? aditivosFormalizados)
    {
        var maiorNovaData = aditivosFormalizados?
            .Where(a => a.NovaDataFimVigencia is not null)
            .Select(a => a.NovaDataFimVigencia!.Value)
            .DefaultIfEmpty(contrato.DataFimVigenciaOriginal)
            .Max() ?? contrato.DataFimVigenciaOriginal;

        return maiorNovaData > contrato.DataFimVigenciaOriginal ? maiorNovaData : contrato.DataFimVigenciaOriginal;
    }

    private async Task<Dictionary<int, List<Aditivo>>> ObterAditivosFormalizadosPorContratoAsync(IEnumerable<int> contratoIds)
    {
        var aditivos = await _context.Aditivos
            .Where(a => contratoIds.Contains(a.ContratoId) && a.Status == AditivoStatus.Formalizado)
            .ToListAsync();

        return aditivos.GroupBy(a => a.ContratoId).ToDictionary(g => g.Key, g => g.ToList());
    }

    private async Task<Contrato> BuscarOuFalhar(int id)
    {
        var contrato = await _context.Contratos.FindAsync(id);
        if (contrato is null)
        {
            throw new NotFoundException($"Contrato {id} não encontrado.");
        }

        return contrato;
    }

    private async Task<Contrato> BuscarComItensOuFalhar(int id)
    {
        var contrato = await _context.Contratos
            .Include(c => c.Fornecedor)
            .Include(c => c.Itens)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (contrato is null)
        {
            throw new NotFoundException($"Contrato {id} não encontrado.");
        }

        return contrato;
    }

    private static ContratoDto ParaDto(Contrato c, List<Aditivo>? aditivosFormalizados) => new()
    {
        Id = c.Id,
        Numero = c.Numero,
        FornecedorId = c.FornecedorId,
        FornecedorNome = c.Fornecedor.Nome,
        Objeto = c.Objeto,
        Natureza = c.Natureza,
        DataAssinatura = c.DataAssinatura,
        DataInicioVigencia = c.DataInicioVigencia,
        DataFimVigenciaOriginal = c.DataFimVigenciaOriginal,
        DataFimVigenciaAtual = CalcularVigenciaFimAtual(c, aditivosFormalizados),
        ValorOriginal = c.ValorOriginal,
        ValorAtual = CalcularValorAtual(c, aditivosFormalizados),
        Status = c.Status,
        Observacoes = c.Observacoes,
        QuantidadeItens = c.Itens.Count,
        DataCriacao = c.DataCriacao,
        DataAtualizacao = c.DataAtualizacao,
    };

    private static ContratoItemDto ParaItemDto(ContratoItem i) => new()
    {
        Id = i.Id,
        ContratoId = i.ContratoId,
        Codigo = i.Codigo,
        Descricao = i.Descricao,
        Unidade = i.Unidade,
        QuantidadeContratada = i.QuantidadeContratada,
        ValorUnitario = i.ValorUnitario,
        ValorTotal = i.QuantidadeContratada * i.ValorUnitario,
    };

    private static ContratoMedicaoConfigDto ParaMedicaoConfigDto(ContratoMedicaoConfig m) => new()
    {
        TipoMedicao = m.TipoMedicao,
        DiaInicioPeriodo = m.DiaInicioPeriodo,
        DiaFimPeriodo = m.DiaFimPeriodo,
        ExigeBm = m.ExigeBm,
        ExigeAprovacao = m.ExigeAprovacao,
        ExigeAssinatura = m.ExigeAssinatura,
        PermiteProRata = m.PermiteProRata,
        MetodoProRata = m.MetodoProRata,
        DiasAntecedenciaAlerta = m.DiasAntecedenciaAlerta,
    };

    private static ContratoFaturamentoConfigDto ParaFaturamentoConfigDto(ContratoFaturamentoConfig f) => new()
    {
        DiaInicialJanelaNf = f.DiaInicialJanelaNf,
        DiaFinalJanelaNf = f.DiaFinalJanelaNf,
        ExigeBmAprovado = f.ExigeBmAprovado,
        ExigeBmAssinado = f.ExigeBmAssinado,
        PrazoPagamentoDias = f.PrazoPagamentoDias,
    };

    private static AditivoDto ParaAditivoDto(Aditivo a) => new()
    {
        Id = a.Id,
        ContratoId = a.ContratoId,
        Numero = a.Numero,
        Descricao = a.Descricao,
        DataAssinatura = a.DataAssinatura,
        DataEfeito = a.DataEfeito,
        DeltaValor = a.DeltaValor,
        NovaDataFimVigencia = a.NovaDataFimVigencia,
        PercentualReajuste = a.PercentualReajuste,
        Status = a.Status,
        DataFormalizacao = a.DataFormalizacao,
        Observacao = a.Observacao,
        DataCriacao = a.DataCriacao,
        Itens = a.Itens.Select(ParaAditivoItemDto).ToList(),
    };

    private static AditivoItemDto ParaAditivoItemDto(AditivoItem i) => new()
    {
        Id = i.Id,
        ContratoItemId = i.ContratoItemId,
        DescricaoContratoItem = i.ContratoItem?.Descricao,
        DescricaoNovoItem = i.DescricaoNovoItem,
        CodigoNovoItem = i.CodigoNovoItem,
        UnidadeNovoItem = i.UnidadeNovoItem,
        DeltaQuantidade = i.DeltaQuantidade,
        NovoValorUnitario = i.NovoValorUnitario,
    };
}

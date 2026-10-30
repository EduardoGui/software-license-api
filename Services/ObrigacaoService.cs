using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class ObrigacaoService : IObrigacaoService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ObrigacaoService> _logger;
    private readonly IEscopoAcesso? _escopo;
    private readonly IAuditoriaService? _auditoria;

    public ObrigacaoService(
        AppDbContext context, TimeProvider timeProvider, ILogger<ObrigacaoService> logger, IEscopoAcesso? escopo = null, IAuditoriaService? auditoria = null)
    {
        _escopo = escopo;
        _auditoria = auditoria;
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<List<ObrigacaoDto>> GetAllAsync(ObrigacaoFiltroDto filtro)
    {
        var query = MontarConsultaBase();

        if (filtro.CompetenciaDe is not null)
        {
            query = query.Where(o => o.Competencia >= filtro.CompetenciaDe);
        }

        if (filtro.CompetenciaAte is not null)
        {
            query = query.Where(o => o.Competencia <= filtro.CompetenciaAte);
        }

        if (!string.IsNullOrWhiteSpace(filtro.TipoMovimento))
        {
            query = query.Where(o => o.TipoMovimento == filtro.TipoMovimento);
        }

        if (filtro.FornecedorId is not null)
        {
            query = query.Where(o => o.FornecedorId == filtro.FornecedorId);
        }

        if (filtro.ContratoId is not null)
        {
            query = query.Where(o => o.MedicaoBm != null && o.MedicaoBm.ContratoId == filtro.ContratoId);
        }

        if (filtro.OrdemCompraId is not null)
        {
            query = query.Where(o => o.OrdemCompraId == filtro.OrdemCompraId);
        }

        if (filtro.ResponsavelUsuarioId is not null)
        {
            query = query.Where(o => o.ResponsavelUsuarioId == filtro.ResponsavelUsuarioId);
        }

        if (filtro.Pago is not null)
        {
            query = query.Where(o => o.Pago == filtro.Pago);
        }

        if (filtro.Cancelada is not null)
        {
            query = query.Where(o => o.Cancelada == filtro.Cancelada);
        }

        var obrigacoes = await query
            .OrderBy(o => o.Vencimento ?? DateOnly.MaxValue)
            .ThenBy(o => o.FornecedorId)
            .ToListAsync();
        var resumos = await CarregarResumosParcelasAsync(obrigacoes.Where(o => o.OrdemCompraId is not null).Select(o => o.OrdemCompraId!.Value).Distinct().ToList());
        var dtos = obrigacoes.Select(o => ParaDto(o, ObterResumo(resumos, o))).ToList();
        await PreencherExigeAssinaturaMedicoesAsync(dtos);

        if (!string.IsNullOrWhiteSpace(filtro.Etapa))
        {
            dtos = dtos.Where(d => d.Etapa == filtro.Etapa).ToList();
        }

        return dtos;
    }

    public async Task<ObrigacaoDto> GetByIdAsync(int id)
    {
        var obrigacao = await BuscarOuFalhar(id);
        return await ParaDtoAsync(obrigacao);
    }

    public async Task<ObrigacaoDto> UpdateAsync(int id, UpdateObrigacaoDto dto)
    {
        var obrigacao = await BuscarOuFalhar(id);
        var irmas = await BuscarParcelasIrmasAtivasAsync(obrigacao);

        if (dto.ValorPrevisto is not null && dto.ValorPrevisto.Value != obrigacao.ValorPrevisto)
        {
            if (dto.ValorPrevisto.Value <= 0)
            {
                throw new BusinessRuleException("Valor previsto deve ser maior que zero.");
            }

            if (irmas.Count == 0)
            {
                throw new BusinessRuleException("O valor previsto só pode ser alterado em Ordem de Compra com mais de uma parcela.");
            }

            if (obrigacao.Pago)
            {
                throw new BusinessRuleException("Parcela já paga: não é possível alterar o valor previsto.");
            }

            obrigacao.ValorPrevisto = dto.ValorPrevisto.Value;
        }

        obrigacao.DataNf = dto.DataNf;
        obrigacao.NumeroNf = dto.NumeroNf?.Trim();
        obrigacao.ValorNota = dto.ValorNota;
        obrigacao.Vencimento = dto.Vencimento;
        obrigacao.DataRequisicao = dto.DataRequisicao;
        obrigacao.DataAssistPgto = dto.DataAssistPgto;
        obrigacao.DataEnvioFinanceiro = dto.DataEnvioFinanceiro;
        obrigacao.DataPrevistaPagamento = dto.DataPrevistaPagamento;
        obrigacao.Observacoes = dto.Observacoes?.Trim();
        obrigacao.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        // Uma NF só para todos os boletos: nº, data e valor da nota vão para as demais parcelas, e a competência
        // das parcelas segue o mês de emissão da NF.
        if (irmas.Count > 0)
        {
            foreach (var irma in irmas)
            {
                irma.NumeroNf = obrigacao.NumeroNf;
                irma.DataNf = obrigacao.DataNf;
                irma.ValorNota = obrigacao.ValorNota;
                irma.DataAtualizacao = obrigacao.DataAtualizacao;
            }

            if (obrigacao.DataNf is not null)
            {
                var competenciaNf = new DateOnly(obrigacao.DataNf.Value.Year, obrigacao.DataNf.Value.Month, 1);
                obrigacao.Competencia = competenciaNf;
                foreach (var irma in irmas)
                {
                    irma.Competencia = competenciaNf;
                }
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Obrigação {ObrigacaoId} atualizada", obrigacao.Id);

        return await ParaDtoAsync(obrigacao);
    }

    // Registra que o pedido de pagamento foi enviado ao financeiro (hoje) e a data de pagamento combinada.
    // Grava as datas de andamento na origem (OC ou BM): as parcelas de uma mesma OC compartilham as datas.
    public async Task<ObrigacaoDto> AtualizarAndamentoAsync(int id, AtualizarAndamentoObrigacaoDto dto)
    {
        var obrigacao = await BuscarOuFalhar(id);

        if (obrigacao.Cancelada)
        {
            throw new BusinessRuleException("Não é possível alterar o andamento de uma obrigação cancelada.");
        }

        var agora = _timeProvider.GetUtcNow().UtcDateTime;

        if (obrigacao.TipoMovimento == ObrigacaoTipoMovimento.OrdemCompra && obrigacao.OrdemCompra is not null)
        {
            if (dto.DataSolicitacaoEspelho is not null || dto.DataLiberacaoNf is not null)
            {
                throw new BusinessRuleException("Solicitação de espelho e liberação para NF só valem para medições.");
            }

            var oc = obrigacao.OrdemCompra;
            oc.DataEnvioAssinatura = dto.DataEnvioAssinatura;
            oc.DataEnvioFornecedor = dto.DataEnvioFornecedor;
            oc.DataChegadaMaterial = dto.DataChegadaMaterial;
            oc.DataAtualizacao = agora;
        }
        else if (obrigacao.TipoMovimento == ObrigacaoTipoMovimento.Medicao && obrigacao.MedicaoBm is not null)
        {
            if (dto.DataEnvioFornecedor is not null || dto.DataChegadaMaterial is not null)
            {
                throw new BusinessRuleException("Envio ao fornecedor e chegada do material só valem para ordens de compra.");
            }

            if (dto.DataEnvioAssinatura is not null)
            {
                var exigeAssinatura = await _context.ContratoMedicaoConfigs
                    .AnyAsync(c => c.ContratoId == obrigacao.MedicaoBm.ContratoId && c.ExigeAssinatura);
                if (!exigeAssinatura)
                {
                    throw new BusinessRuleException("O contrato desta medição não exige assinatura.");
                }
            }

            var bm = obrigacao.MedicaoBm;
            bm.DataSolicitacaoEspelho = dto.DataSolicitacaoEspelho;
            bm.DataEnvioAssinatura = dto.DataEnvioAssinatura;
            bm.DataLiberacaoNf = dto.DataLiberacaoNf;
            bm.DataAtualizacao = agora;
        }
        else
        {
            throw new BusinessRuleException("Este tipo de obrigação não tem andamento documental.");
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Andamento documental da obrigação {ObrigacaoId} atualizado", obrigacao.Id);

        return await ParaDtoAsync(obrigacao);
    }

    public async Task<ObrigacaoDto> MarcarEnviadaFinanceiroAsync(int id, MarcarEnviadaFinanceiroDto dto)
    {
        var obrigacao = await BuscarOuFalhar(id);

        if (obrigacao.Cancelada)
        {
            throw new BusinessRuleException("Não é possível marcar como enviada ao financeiro uma obrigação cancelada.");
        }

        if (obrigacao.Pago)
        {
            throw new BusinessRuleException("Esta obrigação já está paga.");
        }

        obrigacao.DataEnvioFinanceiro = HorarioBrasilia.Hoje(_timeProvider);
        if (dto.DataPrevistaPagamento is not null)
        {
            obrigacao.DataPrevistaPagamento = dto.DataPrevistaPagamento;
        }

        obrigacao.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Obrigação {ObrigacaoId} marcada como enviada ao financeiro", obrigacao.Id);

        return await ParaDtoAsync(obrigacao);
    }

    public async Task<ObrigacaoDto> MarcarPagaAsync(int id)
    {
        var obrigacao = await BuscarOuFalhar(id);

        if (obrigacao.Cancelada)
        {
            throw new BusinessRuleException("Não é possível marcar como paga uma obrigação cancelada.");
        }

        obrigacao.Pago = true;
        obrigacao.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Obrigação {ObrigacaoId} marcada como paga", obrigacao.Id);

        return await ParaDtoAsync(obrigacao);
    }

    public async Task<ObrigacaoDto> DesmarcarPagaAsync(int id)
    {
        var obrigacao = await BuscarOuFalhar(id);

        obrigacao.Pago = false;
        obrigacao.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Obrigação {ObrigacaoId} desmarcada como paga", obrigacao.Id);

        return await ParaDtoAsync(obrigacao);
    }

    public async Task<ObrigacaoDto> CancelarAsync(int id)
    {
        var obrigacao = await BuscarOuFalhar(id);

        obrigacao.Cancelada = true;
        obrigacao.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Obrigação {ObrigacaoId} cancelada", obrigacao.Id);

        return await ParaDtoAsync(obrigacao);
    }

    public async Task<ObrigacaoDto> ReativarAsync(int id)
    {
        var obrigacao = await BuscarOuFalhar(id);

        obrigacao.Cancelada = false;
        obrigacao.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Obrigação {ObrigacaoId} reativada (cancelamento desfeito)", obrigacao.Id);

        return await ParaDtoAsync(obrigacao);
    }

    // Ordem de Compra paga em várias vezes: cria mais uma obrigação ligada à mesma OC. Copia só o que é do documento
    // fiscal (único) e deixa em branco o que é de cada pagamento (vencimento, requisição, envio, pago).
    public async Task<ObrigacaoDto> AdicionarParcelaAsync(int id, AdicionarParcelaObrigacaoDto dto)
    {
        var origem = await BuscarOuFalhar(id);

        if (origem.OrdemCompraId is null)
        {
            throw new BusinessRuleException("Só é possível adicionar parcela em uma obrigação de Ordem de Compra.");
        }

        if (origem.Cancelada)
        {
            throw new BusinessRuleException("Não é possível adicionar parcela a partir de uma obrigação cancelada.");
        }

        var ordemCompraId = origem.OrdemCompraId.Value;
        var proximoNumero = await _context.Obrigacoes.Where(o => o.OrdemCompraId == ordemCompraId).MaxAsync(o => o.NumeroParcela) + 1;

        // O valor da nova parcela sai da original (que ainda não foi paga), para a soma continuar fechando.
        if (!origem.Pago && dto.Valor < origem.ValorPrevisto)
        {
            origem.ValorPrevisto -= dto.Valor;
        }

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var competencia = origem.DataNf is null ? origem.Competencia : new DateOnly(origem.DataNf.Value.Year, origem.DataNf.Value.Month, 1);
        origem.Competencia = competencia;
        origem.DataAtualizacao = agora;

        var nova = new Obrigacao
        {
            TipoMovimento = origem.TipoMovimento,
            OrdemCompraId = ordemCompraId,
            NumeroParcela = proximoNumero,
            FornecedorId = origem.FornecedorId,
            Competencia = competencia,
            ValorPrevisto = dto.Valor,
            DataNf = origem.DataNf,
            NumeroNf = origem.NumeroNf,
            ValorNota = origem.ValorNota,
            // A parcela continua com quem acompanha a OC (senão sumiria para o responsável que acabou de criá-la).
            ResponsavelUsuarioId = origem.ResponsavelUsuarioId,
            DataAtribuicao = origem.DataAtribuicao,
            DataCriacao = agora,
            DataAtualizacao = agora,
        };
        _context.Obrigacoes.Add(nova);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Parcela {NumeroParcela} adicionada à Ordem de Compra {OrdemCompraId} (obrigação {ObrigacaoId})", proximoNumero, ordemCompraId, nova.Id);

        return await ParaDtoAsync(await BuscarOuFalhar(nova.Id));
    }

    // --- responsável (Administrador atribui a um usuário Administrativo, que passa a ver e operar a obrigação) ---

    public async Task<List<ResponsavelObrigacaoDto>> ListarResponsaveisAsync()
    {
        var hoje = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
        var usuarioIds = await IdsDeUsuariosAdministrativosAsync();
        var usuarios = await _context.Usuarios.AsNoTracking().Where(u => usuarioIds.Contains(u.Id)).OrderBy(u => u.Nome).ToListAsync();

        return usuarios
            .Where(u => UsuarioStatus.Calcular(u, hoje) != UsuarioStatus.Inativo)
            .Select(u => new ResponsavelObrigacaoDto { UsuarioId = u.Id, Nome = u.Nome })
            .ToList();
    }

    public async Task<ObrigacaoDto> AtribuirResponsavelAsync(int id, AtribuirResponsavelObrigacaoDto dto, int? usuarioIdAtor)
    {
        var obrigacao = await BuscarOuFalhar(id);
        var anterior = obrigacao.Responsavel?.Nome;

        if (dto.UsuarioId is null)
        {
            obrigacao.ResponsavelUsuarioId = null;
            obrigacao.Responsavel = null;
            obrigacao.DataAtribuicao = null;
        }
        else
        {
            var usuarioId = dto.UsuarioId.Value;
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId)
                ?? throw new NotFoundException($"Usuário {usuarioId} não encontrado.");

            var hoje = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
            if (UsuarioStatus.Calcular(usuario, hoje) == UsuarioStatus.Inativo)
            {
                throw new BusinessRuleException("O usuário escolhido está inativo.");
            }

            if (!(await IdsDeUsuariosAdministrativosAsync()).Contains(usuarioId))
            {
                throw new BusinessRuleException("O responsável precisa ser um usuário com perfil Administrativo.");
            }

            obrigacao.ResponsavelUsuarioId = usuarioId;
            obrigacao.Responsavel = usuario;
            obrigacao.DataAtribuicao = _timeProvider.GetUtcNow().UtcDateTime;
        }

        obrigacao.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;
        await _context.SaveChangesAsync();

        var novo = obrigacao.Responsavel?.Nome;
        _logger.LogInformation("Responsável da obrigação {ObrigacaoId} alterado de {Anterior} para {Novo}", id, anterior ?? "(ninguém)", novo ?? "(ninguém)");
        if (_auditoria is not null)
        {
            await _auditoria.RegistrarAsync(
                usuarioIdAtor, LogAuditoriaEntidade.Obrigacao, id, novo is null ? "ResponsavelRemovido" : "ResponsavelAtribuido",
                $"{anterior ?? "(ninguém)"} → {novo ?? "(ninguém)"}");
        }

        return await ParaDtoAsync(obrigacao);
    }

    // Colaboradores (Usuario) cuja conta de acesso tem o papel Administrativo.
    private async Task<List<int>> IdsDeUsuariosAdministrativosAsync() =>
        await (from c in _context.Users
               join ur in _context.UserRoles on c.Id equals ur.UserId
               join r in _context.Roles on ur.RoleId equals r.Id
               where r.Name == Roles.Administrativo && c.UsuarioId != null
               select c.UsuarioId!.Value)
            .Distinct()
            .ToListAsync();

    // --- anexos do documento de origem (medição, OC ou despesa avulsa), acessíveis a quem enxerga a obrigação ---

    public async Task<List<AnexoDto>> ListarAnexosAsync(int id)
    {
        var o = await BuscarOuFalhar(id);

        if (o.MedicaoBmId is { } medicaoId)
        {
            return await _context.MedicaoBmAnexos.AsNoTracking().Where(a => a.MedicaoBmId == medicaoId)
                .OrderByDescending(a => a.DataUpload)
                .Select(a => new AnexoDto { Id = a.Id, NomeArquivo = a.NomeArquivo, TipoConteudo = a.TipoConteudo, Tamanho = a.Tamanho, DataUpload = a.DataUpload })
                .ToListAsync();
        }

        if (o.OrdemCompraId is { } ordemId)
        {
            return await _context.OrdemCompraAnexos.AsNoTracking().Where(a => a.OrdemCompraId == ordemId)
                .OrderByDescending(a => a.DataUpload)
                .Select(a => new AnexoDto { Id = a.Id, NomeArquivo = a.NomeArquivo, TipoConteudo = a.TipoConteudo, Tamanho = a.Tamanho, DataUpload = a.DataUpload })
                .ToListAsync();
        }

        var despesaId = o.DespesaAvulsaId ?? throw new BusinessRuleException("A obrigação não tem documento de origem.");
        return await _context.DespesaAvulsaAnexos.AsNoTracking().Where(a => a.DespesaAvulsaId == despesaId)
            .OrderByDescending(a => a.DataUpload)
            .Select(a => new AnexoDto { Id = a.Id, NomeArquivo = a.NomeArquivo, TipoConteudo = a.TipoConteudo, Tamanho = a.Tamanho, DataUpload = a.DataUpload })
            .ToListAsync();
    }

    public async Task<AnexoDto> AdicionarAnexoAsync(int id, AdicionarAnexoDto dto)
    {
        var o = await BuscarOuFalhar(id);
        AnexoValidator.Validar(dto.TipoConteudo, dto.Conteudo.Length);
        var agora = _timeProvider.GetUtcNow().UtcDateTime;

        AnexoDto Resultado(int anexoId) => new()
        {
            Id = anexoId, NomeArquivo = dto.NomeArquivo, TipoConteudo = dto.TipoConteudo, Tamanho = dto.Conteudo.Length, DataUpload = agora,
        };

        if (o.MedicaoBmId is { } medicaoId)
        {
            var anexo = new MedicaoBmAnexo
            {
                MedicaoBmId = medicaoId, NomeArquivo = dto.NomeArquivo, TipoConteudo = dto.TipoConteudo, Tamanho = dto.Conteudo.Length,
                Conteudo = dto.Conteudo, DataUpload = agora,
            };
            _context.MedicaoBmAnexos.Add(anexo);
            await _context.SaveChangesAsync();
            return Resultado(anexo.Id);
        }

        if (o.OrdemCompraId is { } ordemId)
        {
            var anexo = new OrdemCompraAnexo
            {
                OrdemCompraId = ordemId, NomeArquivo = dto.NomeArquivo, TipoConteudo = dto.TipoConteudo, Tamanho = dto.Conteudo.Length,
                Conteudo = dto.Conteudo, DataUpload = agora,
            };
            _context.OrdemCompraAnexos.Add(anexo);
            await _context.SaveChangesAsync();
            return Resultado(anexo.Id);
        }

        var despesaId = o.DespesaAvulsaId ?? throw new BusinessRuleException("A obrigação não tem documento de origem.");
        var anexoDespesa = new DespesaAvulsaAnexo
        {
            DespesaAvulsaId = despesaId, NomeArquivo = dto.NomeArquivo, TipoConteudo = dto.TipoConteudo, Tamanho = dto.Conteudo.Length,
            Conteudo = dto.Conteudo, DataUpload = agora,
        };
        _context.DespesaAvulsaAnexos.Add(anexoDespesa);
        await _context.SaveChangesAsync();
        return Resultado(anexoDespesa.Id);
    }

    public async Task<AnexoArquivoDto> ObterAnexoAsync(int id, int anexoId)
    {
        var o = await BuscarOuFalhar(id);

        if (o.MedicaoBmId is { } medicaoId)
        {
            var a = await _context.MedicaoBmAnexos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == anexoId && x.MedicaoBmId == medicaoId)
                ?? throw new NotFoundException($"Anexo {anexoId} não encontrado.");
            return new AnexoArquivoDto { NomeArquivo = a.NomeArquivo, TipoConteudo = a.TipoConteudo, Conteudo = a.Conteudo };
        }

        if (o.OrdemCompraId is { } ordemId)
        {
            var a = await _context.OrdemCompraAnexos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == anexoId && x.OrdemCompraId == ordemId)
                ?? throw new NotFoundException($"Anexo {anexoId} não encontrado.");
            return new AnexoArquivoDto { NomeArquivo = a.NomeArquivo, TipoConteudo = a.TipoConteudo, Conteudo = a.Conteudo };
        }

        var despesaId = o.DespesaAvulsaId ?? throw new BusinessRuleException("A obrigação não tem documento de origem.");
        var d = await _context.DespesaAvulsaAnexos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == anexoId && x.DespesaAvulsaId == despesaId)
            ?? throw new NotFoundException($"Anexo {anexoId} não encontrado.");
        return new AnexoArquivoDto { NomeArquivo = d.NomeArquivo, TipoConteudo = d.TipoConteudo, Conteudo = d.Conteudo };
    }

    public async Task ExcluirAnexoAsync(int id, int anexoId)
    {
        var o = await BuscarOuFalhar(id);

        if (o.MedicaoBmId is { } medicaoId)
        {
            var a = await _context.MedicaoBmAnexos.FirstOrDefaultAsync(x => x.Id == anexoId && x.MedicaoBmId == medicaoId)
                ?? throw new NotFoundException($"Anexo {anexoId} não encontrado.");
            _context.MedicaoBmAnexos.Remove(a);
        }
        else if (o.OrdemCompraId is { } ordemId)
        {
            var a = await _context.OrdemCompraAnexos.FirstOrDefaultAsync(x => x.Id == anexoId && x.OrdemCompraId == ordemId)
                ?? throw new NotFoundException($"Anexo {anexoId} não encontrado.");
            _context.OrdemCompraAnexos.Remove(a);
        }
        else
        {
            var despesaId = o.DespesaAvulsaId ?? throw new BusinessRuleException("A obrigação não tem documento de origem.");
            var a = await _context.DespesaAvulsaAnexos.FirstOrDefaultAsync(x => x.Id == anexoId && x.DespesaAvulsaId == despesaId)
                ?? throw new NotFoundException($"Anexo {anexoId} não encontrado.");
            _context.DespesaAvulsaAnexos.Remove(a);
        }

        await _context.SaveChangesAsync();
    }

    private async Task<List<Obrigacao>> BuscarParcelasIrmasAtivasAsync(Obrigacao obrigacao)
    {
        if (obrigacao.OrdemCompraId is null)
        {
            return [];
        }

        return await _context.Obrigacoes
            .Where(o => o.OrdemCompraId == obrigacao.OrdemCompraId && o.Id != obrigacao.Id && !o.Cancelada)
            .ToListAsync();
    }

    private readonly record struct ResumoParcelas(int Total, decimal Soma, decimal Referencia);

    private async Task<Dictionary<int, ResumoParcelas>> CarregarResumosParcelasAsync(List<int> ordemCompraIds)
    {
        var resumos = new Dictionary<int, ResumoParcelas>();
        if (ordemCompraIds.Count == 0)
        {
            return resumos;
        }

        var ativas = await _context.Obrigacoes.AsNoTracking()
            .Where(o => o.OrdemCompraId != null && ordemCompraIds.Contains(o.OrdemCompraId.Value) && !o.Cancelada)
            .Select(o => new { OrdemCompraId = o.OrdemCompraId!.Value, o.ValorPrevisto, o.ValorNota })
            .ToListAsync();

        var itens = await _context.OrdemCompraItens.AsNoTracking()
            .Where(i => ordemCompraIds.Contains(i.OrdemCompraId))
            .Select(i => new { i.OrdemCompraId, i.Quantidade, i.ValorUnitario })
            .ToListAsync();
        var totalItensPorOc = itens.GroupBy(i => i.OrdemCompraId).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantidade * i.ValorUnitario));

        foreach (var grupo in ativas.GroupBy(a => a.OrdemCompraId))
        {
            var valorNota = grupo.Max(a => a.ValorNota);
            var referencia = valorNota ?? totalItensPorOc.GetValueOrDefault(grupo.Key);
            resumos[grupo.Key] = new ResumoParcelas(grupo.Count(), grupo.Sum(a => a.ValorPrevisto), referencia);
        }

        return resumos;
    }

    private static ResumoParcelas? ObterResumo(Dictionary<int, ResumoParcelas> resumos, Obrigacao o) =>
        o.OrdemCompraId is not null && resumos.TryGetValue(o.OrdemCompraId.Value, out var r) ? r : null;

    private async Task<ObrigacaoDto> ParaDtoAsync(Obrigacao o)
    {
        var resumos = o.OrdemCompraId is null ? [] : await CarregarResumosParcelasAsync([o.OrdemCompraId.Value]);
        var dto = ParaDto(o, ObterResumo(resumos, o));
        await PreencherExigeAssinaturaMedicoesAsync([dto]);
        return dto;
    }

    // Na medição, exigir assinatura depende da configuração do contrato (sem configuração = não exige).
    private async Task PreencherExigeAssinaturaMedicoesAsync(List<ObrigacaoDto> dtos)
    {
        var contratoIds = dtos.Where(d => d.ContratoId is not null).Select(d => d.ContratoId!.Value).Distinct().ToList();
        if (contratoIds.Count == 0)
        {
            return;
        }

        var exigem = await _context.ContratoMedicaoConfigs
            .Where(c => contratoIds.Contains(c.ContratoId) && c.ExigeAssinatura)
            .Select(c => c.ContratoId)
            .ToListAsync();

        foreach (var dto in dtos.Where(d => d.ContratoId is not null))
        {
            dto.ExigeAssinatura = exigem.Contains(dto.ContratoId!.Value);
        }
    }

    // Perfil Administrativo só enxerga obrigações cuja OC/despesa de origem foi criada pela administração ou que o
    // Administrador atribuiu a ele (medições e registros antigos ficam de fora); as demais respondem como inexistentes.
    private IQueryable<Obrigacao> MontarConsultaBase()
    {
        var query = _context.Obrigacoes
            .Include(o => o.Fornecedor)
            .Include(o => o.Responsavel)
            .Include(o => o.MedicaoBm!).ThenInclude(m => m.Contrato)
            .Include(o => o.OrdemCompra!).ThenInclude(oc => oc.CriadoPorUsuario)
            .Include(o => o.DespesaAvulsa!).ThenInclude(d => d.CriadoPorUsuario)
            .AsQueryable();

        if (_escopo?.RestritoAAdministracao != true)
        {
            return query;
        }

        var meuId = _escopo.UsuarioId;
        return query.Where(o => (o.OrdemCompra != null && o.OrdemCompra.CriadaPelaAdministracao)
            || (o.DespesaAvulsa != null && o.DespesaAvulsa.CriadaPelaAdministracao)
            || (meuId != null && o.ResponsavelUsuarioId == meuId));
    }

    private async Task<Obrigacao> BuscarOuFalhar(int id)
    {
        var obrigacao = await MontarConsultaBase().FirstOrDefaultAsync(o => o.Id == id);
        if (obrigacao is null)
        {
            throw new NotFoundException($"Obrigação {id} não encontrada.");
        }

        return obrigacao;
    }

    private static string CalcularEtapa(Obrigacao o)
    {
        if (o.Cancelada) return ObrigacaoEtapa.Cancelada;
        if (o.Pago) return ObrigacaoEtapa.Pago;
        if (o.DataAssistPgto is not null || o.DataEnvioFinanceiro is not null) return ObrigacaoEtapa.AguardandoPagamento;
        if (o.DataRequisicao is not null) return ObrigacaoEtapa.CriarAssistPgto;
        if (o.DataNf is not null) return ObrigacaoEtapa.CriarRequisicao;
        if (o.TipoMovimento == ObrigacaoTipoMovimento.Medicao && o.MedicaoBm?.Status != MedicaoBmStatus.Aprovado)
        {
            return ObrigacaoEtapa.AguardarAprovacaoBm;
        }

        return ObrigacaoEtapa.AguardarNf;
    }

    private static ObrigacaoDto ParaDto(Obrigacao o, ResumoParcelas? resumo) => new()
    {
        Id = o.Id,
        TipoMovimento = o.TipoMovimento,
        MedicaoBmId = o.MedicaoBmId,
        MedicaoBmNumero = o.MedicaoBm?.Numero,
        MedicaoBmNumeroReferencia = o.MedicaoBm?.NumeroReferencia,
        ContratoId = o.MedicaoBm?.ContratoId,
        ContratoNumero = o.MedicaoBm?.Contrato?.Numero,
        OrdemCompraId = o.OrdemCompraId,
        OrdemCompraNumero = o.OrdemCompra?.Numero,
        DespesaAvulsaId = o.DespesaAvulsaId,
        DespesaAvulsaDescricao = o.DespesaAvulsa?.Descricao,
        FornecedorId = o.FornecedorId,
        FornecedorNome = o.Fornecedor.Nome,
        Competencia = o.Competencia,
        ValorPrevisto = o.ValorPrevisto,
        NumeroParcela = o.NumeroParcela,
        TotalParcelas = Math.Max(1, resumo?.Total ?? 1),
        SomaParcelas = resumo is { Total: > 1 } ? resumo.Value.Soma : null,
        ValorReferenciaParcelas = resumo is { Total: > 1 } ? resumo.Value.Referencia : null,
        DataNf = o.DataNf,
        NumeroNf = o.NumeroNf,
        ValorNota = o.ValorNota,
        Vencimento = o.Vencimento,
        DataRequisicao = o.DataRequisicao,
        DataAssistPgto = o.DataAssistPgto,
        DataEnvioFinanceiro = o.DataEnvioFinanceiro,
        DataPrevistaPagamento = o.DataPrevistaPagamento,
        Pago = o.Pago,
        Cancelada = o.Cancelada,
        Etapa = CalcularEtapa(o),
        CriadoPorNome = o.OrdemCompra?.CriadoPorUsuario?.Nome ?? o.DespesaAvulsa?.CriadoPorUsuario?.Nome,
        ResponsavelUsuarioId = o.ResponsavelUsuarioId,
        ResponsavelNome = o.Responsavel?.Nome,
        DataAtribuicao = o.DataAtribuicao,
        DataSolicitacaoEspelho = o.MedicaoBm?.DataSolicitacaoEspelho,
        DataEnvioAssinatura = o.OrdemCompra?.DataEnvioAssinatura ?? o.MedicaoBm?.DataEnvioAssinatura,
        DataEnvioFornecedor = o.OrdemCompra?.DataEnvioFornecedor,
        DataChegadaMaterial = o.OrdemCompra?.DataChegadaMaterial,
        DataLiberacaoNf = o.MedicaoBm?.DataLiberacaoNf,
        ExigeAssinatura = o.TipoMovimento == ObrigacaoTipoMovimento.OrdemCompra,
        OrdemCompraAssinada = o.OrdemCompra?.Status == OrdemCompraStatus.Assinada,
        Observacoes = o.Observacoes,
        DataCriacao = o.DataCriacao,
        DataAtualizacao = o.DataAtualizacao,
    };
}

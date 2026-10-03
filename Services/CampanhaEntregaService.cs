using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public class CampanhaEntregaService : ICampanhaEntregaService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CampanhaEntregaService> _logger;
    private readonly IEmailSender _emailSender;
    private readonly IAuditoriaService _auditoriaService;
    private readonly string _frontendBaseUrl;

    public CampanhaEntregaService(
        AppDbContext context,
        TimeProvider timeProvider,
        ILogger<CampanhaEntregaService> logger,
        IEmailSender emailSender,
        IAuditoriaService auditoriaService,
        IConfiguration configuration)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
        _emailSender = emailSender;
        _auditoriaService = auditoriaService;
        _frontendBaseUrl = configuration["Frontend:BaseUrl"] ?? "http://localhost:4200";
    }

    public async Task<List<CampanhaEntregaDto>> GetAllAsync(CampanhaEntregaFiltroDto filtro)
    {
        var query = _context.CampanhasEntrega.Include(c => c.Itens).Include(c => c.Entregas).ThenInclude(e => e.Itens).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Nome))
        {
            query = query.Where(c => EF.Functions.ILike(c.Nome, $"%{filtro.Nome}%"));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Status))
        {
            query = query.Where(c => c.Status == filtro.Status);
        }

        var campanhas = await query.OrderByDescending(c => c.DataCriacao).ToListAsync();
        return campanhas.Select(ParaDto).ToList();
    }

    public async Task<CampanhaEntregaDto> GetByIdAsync(int id)
    {
        var campanha = await BuscarCampanhaOuFalhar(id);
        return ParaDto(campanha);
    }

    public async Task<CampanhaEntregaDto> CreateAsync(CreateCampanhaEntregaDto dto)
    {
        if (!CampanhaEntregaTipo.Validos.Contains(dto.Tipo))
        {
            throw new BusinessRuleException("Tipo de campanha inválido. Use Kit ou ItemAItem.");
        }

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var campanha = new CampanhaEntrega
        {
            Nome = dto.Nome.Trim(),
            Descricao = dto.Descricao?.Trim(),
            Tipo = dto.Tipo,
            Status = CampanhaEntregaStatus.Rascunho,
            DataCriacao = agora,
            DataAtualizacao = agora,
        };

        _context.CampanhasEntrega.Add(campanha);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Campanha de entrega {CampanhaId} criada", campanha.Id);

        return ParaDto(campanha);
    }

    public async Task<CampanhaEntregaDto> UpdateAsync(int id, UpdateCampanhaEntregaDto dto)
    {
        var campanha = await BuscarCampanhaOuFalhar(id);

        if (campanha.Status == CampanhaEntregaStatus.Cancelada)
        {
            throw new BusinessRuleException("Não é possível editar uma campanha cancelada.");
        }

        campanha.Nome = dto.Nome.Trim();
        campanha.Descricao = dto.Descricao?.Trim();
        campanha.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Campanha de entrega {CampanhaId} atualizada", campanha.Id);

        return ParaDto(campanha);
    }

    public async Task DeleteAsync(int id)
    {
        var campanha = await BuscarCampanhaOuFalhar(id);

        if (campanha.Status != CampanhaEntregaStatus.Rascunho)
        {
            throw new BusinessRuleException("Só é possível excluir uma campanha enquanto estiver em Rascunho.");
        }

        _context.CampanhasEntrega.Remove(campanha);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Campanha de entrega {CampanhaId} excluída", id);
    }

    public async Task<CampanhaEntregaDto> CancelarAsync(int id)
    {
        var campanha = await BuscarCampanhaOuFalhar(id);

        if (campanha.Status == CampanhaEntregaStatus.Cancelada)
        {
            throw new BusinessRuleException("Esta campanha já está cancelada.");
        }

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        campanha.Status = CampanhaEntregaStatus.Cancelada;
        campanha.DataAtualizacao = agora;

        foreach (var entrega in campanha.Entregas)
        {
            if (entrega.Status is EntregaStatus.Pendente or EntregaStatus.EmailEnviado)
            {
                entrega.Status = EntregaStatus.Cancelado;
                entrega.DataAtualizacao = agora;
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Campanha de entrega {CampanhaId} cancelada", id);

        return ParaDto(campanha);
    }

    public async Task<CampanhaEntregaDto> EncerrarAsync(int id)
    {
        var campanha = await BuscarCampanhaOuFalhar(id);

        if (campanha.Status is CampanhaEntregaStatus.Cancelada or CampanhaEntregaStatus.Encerrada)
        {
            throw new BusinessRuleException("Esta campanha não pode ser encerrada.");
        }

        campanha.Status = CampanhaEntregaStatus.Encerrada;
        campanha.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Campanha de entrega {CampanhaId} encerrada", id);

        return ParaDto(campanha);
    }

    public async Task<CampanhaEntregaResumoDto> ObterResumoAsync(int campanhaId)
    {
        await BuscarCampanhaOuFalhar(campanhaId);

        var contagens = await _context.Entregas
            .Where(e => e.CampanhaEntregaId == campanhaId)
            .GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Quantidade = g.Count() })
            .ToListAsync();

        int ContarPor(string status) => contagens.FirstOrDefault(c => c.Status == status)?.Quantidade ?? 0;

        return new CampanhaEntregaResumoDto
        {
            Total = contagens.Where(c => c.Status != EntregaStatus.Cancelado).Sum(c => c.Quantidade),
            Pendentes = ContarPor(EntregaStatus.Pendente),
            EmailEnviado = ContarPor(EntregaStatus.EmailEnviado),
            Confirmados = ContarPor(EntregaStatus.Confirmado),
            Divergencias = ContarPor(EntregaStatus.Divergencia),
            Cancelados = ContarPor(EntregaStatus.Cancelado),
        };
    }

    public async Task<List<ColaboradorDisponivelDto>> ListarColaboradoresDisponiveisAsync(int campanhaId, ColaboradorDisponivelFiltroDto filtro)
    {
        await BuscarCampanhaOuFalhar(campanhaId);

        var hoje = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

        // Entrega cancelada não conta: o colaborador volta a ser elegível pra campanha.
        var idsNaCampanha = _context.Entregas
            .Where(e => e.CampanhaEntregaId == campanhaId && e.Status != EntregaStatus.Cancelado)
            .Select(e => e.UsuarioId);

        var query = _context.Usuarios
            .Include(u => u.Setor)
            .Where(u => !idsNaCampanha.Contains(u.Id))
            .Where(u => u.DataFim == null || u.DataFim > hoje);

        if (!string.IsNullOrWhiteSpace(filtro.Nome))
        {
            query = query.Where(u => EF.Functions.ILike(u.Nome, $"%{filtro.Nome}%"));
        }

        if (filtro.SetorId is not null)
        {
            query = query.Where(u => u.SetorId == filtro.SetorId);
        }

        var usuarios = await query.OrderBy(u => u.Nome).ToListAsync();

        return usuarios.Select(u => new ColaboradorDisponivelDto
        {
            Id = u.Id,
            Nome = u.Nome,
            Email = u.Email,
            SetorNome = u.Setor?.Nome,
        }).ToList();
    }

    public async Task<List<EntregaDto>> ListarEntregasAsync(int campanhaId, EntregaFiltroDto filtro)
    {
        await BuscarCampanhaOuFalhar(campanhaId);

        var query = MontarConsultaEntregas().Where(e => e.CampanhaEntregaId == campanhaId);

        if (filtro.UsuarioId is not null)
        {
            query = query.Where(e => e.UsuarioId == filtro.UsuarioId);
        }

        if (filtro.SetorId is not null)
        {
            query = query.Where(e => e.Usuario.SetorId == filtro.SetorId);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Item))
        {
            query = query.Where(e => e.Itens.Any(i => EF.Functions.ILike(i.Descricao, $"%{filtro.Item}%")));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Status))
        {
            query = query.Where(e => e.Status == filtro.Status);
        }
        else
        {
            // Entrega cancelada não entra na lista por padrão (só aparece filtrando por status Cancelado).
            query = query.Where(e => e.Status != EntregaStatus.Cancelado);
        }

        var entregas = await query.OrderBy(e => e.Usuario.Nome).ToListAsync();
        return entregas.Select(ParaEntregaDto).ToList();
    }

    public async Task<EntregaDto> ObterEntregaAsync(int campanhaId, int entregaId)
    {
        var entrega = await BuscarEntregaOuFalhar(campanhaId, entregaId);
        return ParaEntregaDto(entrega);
    }

    public async Task<EntregaDto> AdicionarEntregaAsync(int campanhaId, CreateEntregaDto dto)
    {
        var campanha = await BuscarCampanhaOuFalhar(campanhaId);
        ValidarCampanhaAberta(campanha);
        ValidarCampanhaTemItens(campanha);

        var usuario = await _context.Usuarios.FindAsync(dto.UsuarioId)
            ?? throw new NotFoundException($"Colaborador {dto.UsuarioId} não encontrado.");

        List<EntregaItem> itens;
        var quantidadeKits = dto.QuantidadeKits;
        if (campanha.Tipo == CampanhaEntregaTipo.ItemAItem)
        {
            if (dto.Itens is null || dto.Itens.Count == 0)
            {
                throw new BusinessRuleException("Escolha ao menos um item para o colaborador.");
            }

            quantidadeKits = 1;
            itens = ResolverItensDaEntrega(campanha, dto.Itens, null);
        }
        else
        {
            if (dto.Itens is { Count: > 0 })
            {
                throw new BusinessRuleException("Campanha do tipo Kit não recebe itens escolhidos — os itens do kit são copiados da campanha.");
            }

            ValidarEstoque(campanha, PedidoDosItensPadrao(campanha, quantidadeKits, 1));
            itens = CopiarItensDaCampanha(campanha, quantidadeKits);
        }

        // Um colaborador pode ter mais de uma entrega na mesma campanha (ex.: diretor/gerente que já
        // recebeu a dele e agora precisa de mais kits em nome dele pra repassar a clientes) - cada
        // chamada aqui sempre cria uma entrega nova, sem checar se ele já está na campanha.
        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var entrega = new Entrega
        {
            CampanhaEntregaId = campanhaId,
            UsuarioId = usuario.Id,
            EmailDestino = usuario.Email,
            Status = EntregaStatus.Pendente,
            QuantidadeKits = quantidadeKits,
            Observacao = string.IsNullOrWhiteSpace(dto.Observacao) ? null : dto.Observacao.Trim(),
            DataCriacao = agora,
            DataAtualizacao = agora,
            Itens = itens,
        };

        _context.Entregas.Add(entrega);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Entrega {EntregaId} adicionada à campanha {CampanhaId} para o colaborador {UsuarioId}", entrega.Id, campanhaId, usuario.Id);

        entrega.Usuario = usuario;
        return ParaEntregaDto(entrega);
    }

    public async Task<List<EntregaDto>> AdicionarEntregasLoteAsync(int campanhaId, CreateEntregaLoteDto dto)
    {
        var campanha = await BuscarCampanhaOuFalhar(campanhaId);
        ValidarCampanhaAberta(campanha);
        ValidarCampanhaTemItens(campanha);

        if (campanha.Tipo == CampanhaEntregaTipo.ItemAItem)
        {
            throw new BusinessRuleException("Nesta campanha (item a item) adicione um colaborador por vez, escolhendo os itens dele.");
        }

        var idsJaNaCampanha = await _context.Entregas
            .Where(e => e.CampanhaEntregaId == campanhaId && e.Status != EntregaStatus.Cancelado)
            .Select(e => e.UsuarioId)
            .ToListAsync();

        var idsNovos = dto.UsuarioIds.Distinct().Except(idsJaNaCampanha).ToList();
        if (idsNovos.Count == 0)
        {
            return [];
        }

        var usuarios = await _context.Usuarios.Where(u => idsNovos.Contains(u.Id)).ToListAsync();
        ValidarEstoque(campanha, PedidoDosItensPadrao(campanha, dto.QuantidadeKits, usuarios.Count));

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var novasEntregas = new List<Entrega>();

        foreach (var usuario in usuarios)
        {
            var entrega = new Entrega
            {
                CampanhaEntregaId = campanhaId,
                UsuarioId = usuario.Id,
                Usuario = usuario,
                EmailDestino = usuario.Email,
                Status = EntregaStatus.Pendente,
                QuantidadeKits = dto.QuantidadeKits,
                Observacao = string.IsNullOrWhiteSpace(dto.Observacao) ? null : dto.Observacao.Trim(),
                DataCriacao = agora,
                DataAtualizacao = agora,
                Itens = CopiarItensDaCampanha(campanha, dto.QuantidadeKits),
            };
            novasEntregas.Add(entrega);
        }

        _context.Entregas.AddRange(novasEntregas);
        await _context.SaveChangesAsync();

        _logger.LogInformation("{Quantidade} entregas adicionadas em lote à campanha {CampanhaId}", novasEntregas.Count, campanhaId);

        return novasEntregas.Select(ParaEntregaDto).ToList();
    }

    public async Task<CampanhaEntregaDto> AtualizarItensCampanhaAsync(int campanhaId, UpdateCampanhaEntregaItensDto dto)
    {
        var campanha = await BuscarCampanhaOuFalhar(campanhaId);
        var agora = _timeProvider.GetUtcNow().UtcDateTime;

        var chaves = dto.Itens.Select(i => (i.Descricao.Trim().ToUpperInvariant(), (i.Tamanho ?? string.Empty).Trim().ToUpperInvariant())).ToList();
        if (chaves.Distinct().Count() != chaves.Count)
        {
            throw new BusinessRuleException("Há itens repetidos (mesma descrição e tamanho) na lista da campanha.");
        }

        var idsRecebidos = dto.Itens.Where(i => i.Id.HasValue).Select(i => i.Id!.Value).ToHashSet();
        if (idsRecebidos.Any(id => campanha.Itens.All(i => i.Id != id)))
        {
            throw new BusinessRuleException("Um dos itens informados não pertence a esta campanha.");
        }

        // Quanto de cada item do catálogo já está atribuído (entregas não canceladas) - é o que impede
        // apagar um item em uso ou baixar o estoque abaixo do que já foi prometido.
        var atribuidoPorItem = campanha.Entregas
            .Where(e => e.Status != EntregaStatus.Cancelado)
            .SelectMany(e => e.Itens)
            .Where(ei => ei.CampanhaEntregaItemId.HasValue)
            .GroupBy(ei => ei.CampanhaEntregaItemId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(ei => ei.Quantidade));

        foreach (var item in campanha.Itens.Where(i => !idsRecebidos.Contains(i.Id)).ToList())
        {
            if (atribuidoPorItem.ContainsKey(item.Id))
            {
                throw new BusinessRuleException(
                    $"{Rotulo(item)} já está atribuído em entregas desta campanha — cancele essas entregas antes de remover o item.");
            }

            campanha.Itens.Remove(item);
            _context.CampanhaEntregaItens.Remove(item);
        }

        foreach (var input in dto.Itens)
        {
            var tamanho = string.IsNullOrWhiteSpace(input.Tamanho) ? null : input.Tamanho.Trim();

            if (input.Id.HasValue)
            {
                var existente = campanha.Itens.First(i => i.Id == input.Id.Value);
                var atribuido = atribuidoPorItem.GetValueOrDefault(existente.Id);
                if (input.QuantidadeDisponivel.HasValue && input.QuantidadeDisponivel.Value < atribuido)
                {
                    throw new BusinessRuleException(
                        $"O estoque de {Rotulo(existente)} não pode ficar abaixo de {atribuido}, que já está atribuído em entregas.");
                }

                existente.Descricao = input.Descricao.Trim();
                existente.Tamanho = tamanho;
                existente.Quantidade = input.Quantidade;
                existente.Validade = input.Validade;
                existente.QuantidadeDisponivel = input.QuantidadeDisponivel;
            }
            else
            {
                campanha.Itens.Add(new CampanhaEntregaItem
                {
                    Descricao = input.Descricao.Trim(),
                    Tamanho = tamanho,
                    Quantidade = input.Quantidade,
                    Validade = input.Validade,
                    QuantidadeDisponivel = input.QuantidadeDisponivel,
                    DataCriacao = agora,
                });
            }
        }

        campanha.DataAtualizacao = agora;

        // Kit: quem já foi adicionado mas ainda está Pendente e sem itens (ex.: adicionado antes de a lista da
        // campanha existir) passa a receber a lista atual. Quem já tem itens próprios não é mexido.
        if (campanha.Tipo == CampanhaEntregaTipo.Kit)
        {
            foreach (var entrega in campanha.Entregas.Where(e => e.Status == EntregaStatus.Pendente && e.Itens.Count == 0))
            {
                entrega.Itens = CopiarItensDaCampanha(campanha);
                entrega.DataAtualizacao = agora;
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Itens da campanha {CampanhaId} atualizados", campanhaId);

        return ParaDto(campanha);
    }

    public async Task<EntregaDto> AtualizarItensEntregaAsync(int campanhaId, int entregaId, UpdateEntregaItensDto dto)
    {
        var campanha = await BuscarCampanhaOuFalhar(campanhaId);
        var entrega = await BuscarEntregaOuFalhar(campanhaId, entregaId);

        if (entrega.Status != EntregaStatus.Pendente)
        {
            throw new BusinessRuleException("Só é possível editar os itens enquanto a entrega estiver Pendente (antes do e-mail ser enviado).");
        }

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var novosItens = ResolverItensDaEntrega(campanha, dto.Itens, entregaId);

        _context.EntregaItens.RemoveRange(entrega.Itens);
        entrega.Itens = novosItens;
        entrega.DataAtualizacao = agora;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Itens da entrega {EntregaId} atualizados", entregaId);

        return ParaEntregaDto(entrega);
    }

    public async Task<EntregaDto> RegistrarEntregaFisicaAsync(int campanhaId, int entregaId, RegistrarEntregaFisicaDto dto)
    {
        var entrega = await BuscarEntregaOuFalhar(campanhaId, entregaId);

        if (entrega.Status == EntregaStatus.Cancelado)
        {
            throw new BusinessRuleException("Não é possível registrar entrega física de uma entrega cancelada.");
        }

        var responsavel = await _context.Usuarios.FindAsync(dto.ResponsavelEntregaId)
            ?? throw new NotFoundException($"Responsável {dto.ResponsavelEntregaId} não encontrado.");

        entrega.DataEntregaFisica = dto.DataEntregaFisica;
        entrega.ResponsavelEntregaId = responsavel.Id;
        entrega.ResponsavelEntrega = responsavel;
        entrega.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Entrega física da Entrega {EntregaId} registrada", entregaId);

        return ParaEntregaDto(entrega);
    }

    public async Task<EntregaDto> CancelarEntregaAsync(int campanhaId, int entregaId)
    {
        var entrega = await BuscarEntregaOuFalhar(campanhaId, entregaId);

        if (entrega.Status is not (EntregaStatus.Pendente or EntregaStatus.EmailEnviado))
        {
            throw new BusinessRuleException("Só é possível cancelar uma entrega enquanto ela estiver Pendente ou com e-mail já enviado (aguardando confirmação).");
        }

        entrega.Status = EntregaStatus.Cancelado;
        entrega.DataAtualizacao = _timeProvider.GetUtcNow().UtcDateTime;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Entrega {EntregaId} cancelada", entregaId);

        return ParaEntregaDto(entrega);
    }

    public async Task<EntregaDto> EnviarEmailAsync(int campanhaId, int entregaId)
    {
        var entrega = await BuscarEntregaOuFalhar(campanhaId, entregaId);

        if (entrega.Itens.Count == 0)
        {
            throw new BusinessRuleException("Adicione ao menos um item antes de enviar o e-mail.");
        }

        if (entrega.Status is not (EntregaStatus.Pendente or EntregaStatus.EmailEnviado))
        {
            throw new BusinessRuleException("Só é possível enviar o e-mail enquanto a entrega estiver Pendente ou aguardando confirmação.");
        }

        var campanha = await BuscarCampanhaOuFalhar(campanhaId);
        var agora = _timeProvider.GetUtcNow().UtcDateTime;

        var (tokenBruto, tokenHash) = GerarToken();
        entrega.TokenHash = tokenHash;
        entrega.DataEnvioEmail = agora;
        entrega.Status = EntregaStatus.EmailEnviado;
        entrega.DataAtualizacao = agora;

        if (campanha.Status == CampanhaEntregaStatus.Rascunho)
        {
            campanha.Status = CampanhaEntregaStatus.EmAndamento;
            campanha.DataAtualizacao = agora;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("E-mail de confirmação da Entrega {EntregaId} enviado", entregaId);
        await _auditoriaService.RegistrarAsync(null, LogAuditoriaEntidade.Entrega, entrega.Id, LogAuditoriaAcao.Enviado);

        var aviso = await EnviarEmailConfirmacaoAsync(entrega, campanha, tokenBruto);

        var resultado = ParaEntregaDto(entrega);
        resultado.AvisoEmail = aviso;
        return resultado;
    }

    public async Task<List<EntregaDto>> ReenviarPendentesAsync(int campanhaId)
    {
        var campanha = await BuscarCampanhaOuFalhar(campanhaId);

        var candidatas = await MontarConsultaEntregas()
            .Where(e => e.CampanhaEntregaId == campanhaId)
            .Where(e => e.Status == EntregaStatus.Pendente || e.Status == EntregaStatus.EmailEnviado)
            .Where(e => e.Itens.Count > 0)
            .ToListAsync();

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var resultados = new List<EntregaDto>();

        foreach (var entrega in candidatas)
        {
            var (tokenBruto, tokenHash) = GerarToken();
            entrega.TokenHash = tokenHash;
            entrega.DataEnvioEmail = agora;
            entrega.Status = EntregaStatus.EmailEnviado;
            entrega.DataAtualizacao = agora;

            if (campanha.Status == CampanhaEntregaStatus.Rascunho)
            {
                campanha.Status = CampanhaEntregaStatus.EmAndamento;
                campanha.DataAtualizacao = agora;
            }

            await _context.SaveChangesAsync();

            var aviso = await EnviarEmailConfirmacaoAsync(entrega, campanha, tokenBruto);

            var dto = ParaEntregaDto(entrega);
            dto.AvisoEmail = aviso;
            resultados.Add(dto);
        }

        _logger.LogInformation("Reenvio em lote concluído para a campanha {CampanhaId}: {Quantidade} entregas processadas", campanhaId, resultados.Count);

        return resultados;
    }

    // A entrega já foi marcada como EmailEnviado antes de chamar isto - uma falha aqui não deve reverter
    // a transição de status, só fica registrada (log + auditoria) e devolve um aviso pra reenvio manual,
    // mesmo espírito do e-mail de aprovação de Reembolso/Nota de Débito.
    private async Task<string?> EnviarEmailConfirmacaoAsync(Entrega entrega, CampanhaEntrega campanha, string tokenBruto)
    {
        if (string.IsNullOrWhiteSpace(entrega.EmailDestino))
        {
            _logger.LogWarning("Entrega {EntregaId} marcada como enviada, mas o colaborador não tem e-mail cadastrado", entrega.Id);
            return "A entrega foi marcada como enviada, mas o colaborador não tem e-mail cadastrado.";
        }

        try
        {
            var link = $"{_frontendBaseUrl}/recebimento/{tokenBruto}";
            var itensHtml = string.Concat(entrega.Itens.Select(i =>
                $"<li>{i.Quantidade}× {i.Descricao}{(string.IsNullOrWhiteSpace(i.Tamanho) ? "" : $" ({i.Tamanho})")}</li>"));
            var assunto = $"Confirmação de recebimento — {campanha.Nome}";
            var corpo = $"""
                <p>Olá, {entrega.Usuario.Nome}!</p>
                <p>Você recebeu os itens abaixo referentes à campanha <strong>{campanha.Nome}</strong>:</p>
                <ul>{itensHtml}</ul>
                <p>Por favor, confirme o recebimento acessando o link abaixo:</p>
                <p><a href="{link}">Confirmar recebimento</a></p>
                """;

            await _emailSender.EnviarAsync(entrega.EmailDestino, assunto, corpo);

            _logger.LogInformation("E-mail de confirmação da Entrega {EntregaId} enviado a {Email}", entrega.Id, entrega.EmailDestino);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Entrega {EntregaId} marcada como enviada, mas o envio do e-mail falhou", entrega.Id);
            await _auditoriaService.RegistrarAsync(
                null, LogAuditoriaEntidade.Entrega, entrega.Id, LogAuditoriaAcao.EmailNaoEnviado, ex.Message);

            return "A entrega foi marcada como enviada, mas o e-mail não pôde ser entregue. Tente reenviar.";
        }
    }

    private static (string TokenBruto, string TokenHash) GerarToken()
    {
        var tokenBruto = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (tokenBruto, HashToken(tokenBruto));
    }

    private static string HashToken(string tokenBruto) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tokenBruto)));

    public async Task<RecebimentoDto> ObterPorTokenAsync(string token, string? ip, string? userAgent)
    {
        var entrega = await BuscarEntregaPorTokenOuFalhar(token);

        if (entrega.DataAcessoLink is null)
        {
            entrega.DataAcessoLink = _timeProvider.GetUtcNow().UtcDateTime;
            entrega.IpAcessoLink = ip;
            entrega.UserAgentAcessoLink = userAgent;
            await _context.SaveChangesAsync();

            await _auditoriaService.RegistrarAsync(null, LogAuditoriaEntidade.Entrega, entrega.Id, LogAuditoriaAcao.AcessoLink, ip);
        }

        return ParaRecebimentoDto(entrega);
    }

    public async Task<RecebimentoDto> ConfirmarPorTokenAsync(string token, string? ip, string? userAgent)
    {
        var entrega = await BuscarEntregaPorTokenOuFalhar(token);
        ValidarEntregaAcionavelPorToken(entrega);

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        entrega.Status = EntregaStatus.Confirmado;
        entrega.DataConfirmacao = agora;
        entrega.IpConfirmacao = ip;
        entrega.UserAgentConfirmacao = userAgent;
        entrega.DataAtualizacao = agora;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Entrega {EntregaId} confirmada pelo colaborador via link público", entrega.Id);
        await _auditoriaService.RegistrarAsync(null, LogAuditoriaEntidade.Entrega, entrega.Id, LogAuditoriaAcao.Confirmado, ip);

        return ParaRecebimentoDto(entrega);
    }

    public async Task<RecebimentoDto> RegistrarDivergenciaPorTokenAsync(string token, RegistrarDivergenciaDto dto, string? ip, string? userAgent)
    {
        if (!TiposDivergenciaValidos.Contains(dto.TipoDivergencia))
        {
            throw new BusinessRuleException("Tipo de divergência inválido.");
        }

        var entrega = await BuscarEntregaPorTokenOuFalhar(token);
        ValidarEntregaAcionavelPorToken(entrega);

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        entrega.Status = EntregaStatus.Divergencia;
        entrega.TipoDivergencia = dto.TipoDivergencia;
        entrega.ObservacaoDivergencia = dto.Observacao?.Trim();
        entrega.DataConfirmacao = agora;
        entrega.IpConfirmacao = ip;
        entrega.UserAgentConfirmacao = userAgent;
        entrega.DataAtualizacao = agora;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Divergência registrada na Entrega {EntregaId} pelo colaborador via link público", entrega.Id);
        await _auditoriaService.RegistrarAsync(null, LogAuditoriaEntidade.Entrega, entrega.Id, LogAuditoriaAcao.DivergenciaRegistrada, dto.TipoDivergencia);

        return ParaRecebimentoDto(entrega);
    }

    private static readonly HashSet<string> TiposDivergenciaValidos =
    [
        TipoDivergenciaEntrega.NaoRecebi, TipoDivergenciaEntrega.QuantidadeIncorreta,
        TipoDivergenciaEntrega.ItemDiferente, TipoDivergenciaEntrega.ItemDanificado, TipoDivergenciaEntrega.Outro,
    ];

    private static void ValidarEntregaAcionavelPorToken(Entrega entrega)
    {
        if (entrega.Status != EntregaStatus.EmailEnviado)
        {
            throw new BusinessRuleException("Este link já foi utilizado ou não é mais válido.");
        }
    }

    private async Task<Entrega> BuscarEntregaPorTokenOuFalhar(string token)
    {
        var hash = HashToken(token);
        var entrega = await MontarConsultaEntregas().FirstOrDefaultAsync(e => e.TokenHash == hash);

        if (entrega is null)
        {
            throw new NotFoundException("Link inválido ou expirado.");
        }

        return entrega;
    }

    public async Task<List<MinhaEntregaDto>> ListarMinhasEntregasAsync(int usuarioId)
    {
        var entregas = await MontarConsultaEntregas()
            .Where(e => e.UsuarioId == usuarioId && (e.Status == EntregaStatus.Confirmado || e.Status == EntregaStatus.Divergencia))
            .OrderByDescending(e => e.DataConfirmacao)
            .ToListAsync();

        return entregas.Select(e => new MinhaEntregaDto
        {
            Id = e.Id,
            CampanhaNome = e.CampanhaEntrega.Nome,
            Status = e.Status,
            DataEntregaFisica = e.DataEntregaFisica,
            DataConfirmacao = e.DataConfirmacao,
            Itens = e.Itens.Select(i => new EntregaItemDto
            {
                Id = i.Id,
                Descricao = i.Descricao,
                Tamanho = i.Tamanho,
                Quantidade = i.Quantidade,
                Validade = i.Validade,
            }).ToList(),
        }).ToList();
    }

    private static RecebimentoDto ParaRecebimentoDto(Entrega e) => new()
    {
        CampanhaNome = e.CampanhaEntrega.Nome,
        UsuarioNome = e.Usuario.Nome,
        DataEntregaFisica = e.DataEntregaFisica,
        Status = e.Status,
        Itens = e.Itens.Select(i => new EntregaItemDto
        {
            Id = i.Id,
            Descricao = i.Descricao,
            Tamanho = i.Tamanho,
            Quantidade = i.Quantidade,
            Validade = i.Validade,
        }).ToList(),
        DataConfirmacao = e.DataConfirmacao,
        TipoDivergencia = e.TipoDivergencia,
        ObservacaoDivergencia = e.ObservacaoDivergencia,
    };

    private static void ValidarCampanhaAberta(CampanhaEntrega campanha)
    {
        if (campanha.Status is CampanhaEntregaStatus.Encerrada or CampanhaEntregaStatus.Cancelada)
        {
            throw new BusinessRuleException("Não é possível adicionar colaboradores a uma campanha encerrada ou cancelada.");
        }
    }

    private static void ValidarCampanhaTemItens(CampanhaEntrega campanha)
    {
        if (campanha.Itens.Count == 0)
        {
            throw new BusinessRuleException("Defina os itens da campanha antes de adicionar colaboradores.");
        }
    }

    // Kit: todos os itens do catálogo entram em cada entrega (× quantidade de kits).
    private static List<EntregaItem> CopiarItensDaCampanha(CampanhaEntrega campanha, int quantidadeKits = 1) => campanha.Itens
        .Select(i => new EntregaItem
        {
            CampanhaEntregaItem = i,
            Descricao = i.Descricao,
            Tamanho = i.Tamanho,
            Quantidade = i.Quantidade * quantidadeKits,
            Validade = i.Validade,
            DataCriacao = DateTime.UtcNow,
        }).ToList();

    private static Dictionary<int, int> PedidoDosItensPadrao(CampanhaEntrega campanha, int quantidadeKits, int quantidadeEntregas) => campanha.Itens
        .ToDictionary(i => i.Id, i => i.Quantidade * quantidadeKits * quantidadeEntregas);

    private static string Rotulo(CampanhaEntregaItem item) =>
        string.IsNullOrWhiteSpace(item.Tamanho) ? item.Descricao : $"{item.Descricao} ({item.Tamanho})";

    // Monta os EntregaItem de uma entrega a partir das linhas informadas, validando pelo tipo da campanha e pelo estoque.
    // Item a item: só itens do catálogo (sem repetir). Kit: texto livre — se casar (descrição + tamanho) com um item do
    // catálogo, liga o vínculo pra o saldo continuar valendo — ou item do catálogo.
    private static List<EntregaItem> ResolverItensDaEntrega(CampanhaEntrega campanha, IReadOnlyList<ItemEntregaInputDto> linhas, int? ignorarEntregaId)
    {
        var itens = new List<EntregaItem>();
        var pedido = new Dictionary<int, int>();
        var idsUsados = new HashSet<int>();
        var agora = DateTime.UtcNow;

        foreach (var linha in linhas)
        {
            CampanhaEntregaItem? catalogo;
            string descricao;
            string? tamanho;
            DateOnly? validade;

            if (linha.CampanhaEntregaItemId.HasValue)
            {
                catalogo = campanha.Itens.FirstOrDefault(i => i.Id == linha.CampanhaEntregaItemId.Value)
                    ?? throw new BusinessRuleException("Um dos itens escolhidos não pertence a esta campanha.");
                descricao = catalogo.Descricao;
                tamanho = catalogo.Tamanho;
                validade = catalogo.Validade;
            }
            else if (campanha.Tipo == CampanhaEntregaTipo.ItemAItem)
            {
                throw new BusinessRuleException("Nesta campanha (item a item) escolha os itens do catálogo da campanha.");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(linha.Descricao))
                {
                    throw new BusinessRuleException("A descrição do item é obrigatória.");
                }

                descricao = linha.Descricao.Trim();
                tamanho = string.IsNullOrWhiteSpace(linha.Tamanho) ? null : linha.Tamanho.Trim();
                validade = linha.Validade;
                catalogo = campanha.Itens.FirstOrDefault(i =>
                    string.Equals(i.Descricao, descricao, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(i.Tamanho ?? string.Empty, tamanho ?? string.Empty, StringComparison.OrdinalIgnoreCase));
            }

            if (catalogo is not null)
            {
                if (campanha.Tipo == CampanhaEntregaTipo.ItemAItem && !idsUsados.Add(catalogo.Id))
                {
                    throw new BusinessRuleException("Não repita o mesmo item — ajuste a quantidade da linha existente.");
                }

                pedido[catalogo.Id] = pedido.GetValueOrDefault(catalogo.Id) + linha.Quantidade;
            }

            itens.Add(new EntregaItem
            {
                CampanhaEntregaItem = catalogo,
                Descricao = descricao,
                Tamanho = tamanho,
                Quantidade = linha.Quantidade,
                Validade = validade,
                DataCriacao = agora,
            });
        }

        ValidarEstoque(campanha, pedido, ignorarEntregaId);
        return itens;
    }

    // Bloqueia quando o pedido (por item do catálogo) passa do saldo: estoque - o que já está atribuído em
    // entregas não canceladas. ignorarEntregaId evita contar a própria entrega que está sendo reeditada.
    private static void ValidarEstoque(CampanhaEntrega campanha, IReadOnlyDictionary<int, int> pedidoPorItem, int? ignorarEntregaId = null)
    {
        foreach (var (itemId, quantidade) in pedidoPorItem)
        {
            var item = campanha.Itens.First(i => i.Id == itemId);
            if (item.QuantidadeDisponivel is null)
            {
                continue;
            }

            var jaAtribuido = campanha.Entregas
                .Where(e => e.Status != EntregaStatus.Cancelado && e.Id != ignorarEntregaId)
                .SelectMany(e => e.Itens)
                .Where(ei => ei.CampanhaEntregaItemId == itemId)
                .Sum(ei => ei.Quantidade);

            var saldo = item.QuantidadeDisponivel.Value - jaAtribuido;
            if (quantidade > saldo)
            {
                throw new BusinessRuleException($"Estoque insuficiente de {Rotulo(item)}: saldo {Math.Max(saldo, 0)}, pedido {quantidade}.");
            }
        }
    }

    private IQueryable<Entrega> MontarConsultaEntregas() => _context.Entregas
        .Include(e => e.Usuario).ThenInclude(u => u.Setor)
        .Include(e => e.ResponsavelEntrega)
        .Include(e => e.Itens)
        .Include(e => e.CampanhaEntrega)
        .AsQueryable();

    private async Task<CampanhaEntrega> BuscarCampanhaOuFalhar(int id)
    {
        var campanha = await _context.CampanhasEntrega
            .Include(c => c.Entregas).ThenInclude(e => e.Itens)
            .Include(c => c.Itens)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (campanha is null)
        {
            throw new NotFoundException($"Campanha de entrega {id} não encontrada.");
        }

        return campanha;
    }

    private async Task<Entrega> BuscarEntregaOuFalhar(int campanhaId, int entregaId)
    {
        var entrega = await MontarConsultaEntregas()
            .FirstOrDefaultAsync(e => e.Id == entregaId && e.CampanhaEntregaId == campanhaId);

        if (entrega is null)
        {
            throw new NotFoundException($"Entrega {entregaId} não encontrada.");
        }

        return entrega;
    }

    private static CampanhaEntregaDto ParaDto(CampanhaEntrega c)
    {
        // Cancelado não consome saldo (a entrega não aconteceu de fato); os demais status (inclusive
        // Divergência) contam, porque o item já saiu fisicamente pra pessoa nesse ponto.
        var itensEntreguesAtivos = c.Entregas.Where(e => e.Status != EntregaStatus.Cancelado).SelectMany(e => e.Itens).ToList();

        return new CampanhaEntregaDto
        {
            Id = c.Id,
            Nome = c.Nome,
            Descricao = c.Descricao,
            Status = c.Status,
            Tipo = c.Tipo,
            DataCriacao = c.DataCriacao,
            DataAtualizacao = c.DataAtualizacao,
            Itens = c.Itens.Select(i =>
            {
                var quantidadeEntregue = itensEntreguesAtivos
                    .Where(ei => ei.CampanhaEntregaItemId == i.Id)
                    .Sum(ei => ei.Quantidade);

                return new CampanhaEntregaItemDto
                {
                    Id = i.Id,
                    Descricao = i.Descricao,
                    Tamanho = i.Tamanho,
                    Quantidade = i.Quantidade,
                    Validade = i.Validade,
                    QuantidadeDisponivel = i.QuantidadeDisponivel,
                    QuantidadeEntregue = quantidadeEntregue,
                    SaldoDisponivel = i.QuantidadeDisponivel.HasValue ? i.QuantidadeDisponivel.Value - quantidadeEntregue : null,
                };
            }).ToList(),
        };
    }

    private static EntregaDto ParaEntregaDto(Entrega e) => new()
    {
        Id = e.Id,
        CampanhaEntregaId = e.CampanhaEntregaId,
        UsuarioId = e.UsuarioId,
        UsuarioNome = e.Usuario.Nome,
        EmailDestino = e.EmailDestino,
        DataEntregaFisica = e.DataEntregaFisica,
        ResponsavelEntregaId = e.ResponsavelEntregaId,
        ResponsavelEntregaNome = e.ResponsavelEntrega?.Nome,
        Status = e.Status,
        DataEnvioEmail = e.DataEnvioEmail,
        DataAcessoLink = e.DataAcessoLink,
        IpAcessoLink = e.IpAcessoLink,
        UserAgentAcessoLink = e.UserAgentAcessoLink,
        DataConfirmacao = e.DataConfirmacao,
        IpConfirmacao = e.IpConfirmacao,
        UserAgentConfirmacao = e.UserAgentConfirmacao,
        TipoDivergencia = e.TipoDivergencia,
        ObservacaoDivergencia = e.ObservacaoDivergencia,
        QuantidadeKits = e.QuantidadeKits,
        Observacao = e.Observacao,
        Itens = e.Itens.Select(i => new EntregaItemDto
        {
            Id = i.Id,
            CampanhaEntregaItemId = i.CampanhaEntregaItemId,
            Descricao = i.Descricao,
            Tamanho = i.Tamanho,
            Quantidade = i.Quantidade,
            Validade = i.Validade,
        }).ToList(),
        DataCriacao = e.DataCriacao,
        DataAtualizacao = e.DataAtualizacao,
    };
}

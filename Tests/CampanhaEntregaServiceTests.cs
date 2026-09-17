using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class CampanhaEntregaServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    private static CampanhaEntregaService CriarService(out AppDbContext context)
    {
        return CriarService(out context, out _);
    }

    private static CampanhaEntregaService CriarService(out AppDbContext context, out FakeEmailSender emailSender)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        context = new AppDbContext(options);
        var configuracao = new ConfigurationBuilder().AddInMemoryCollection().Build();
        emailSender = new FakeEmailSender();
        return new CampanhaEntregaService(
            context, new FakeTimeProvider(Agora), NullLogger<CampanhaEntregaService>.Instance,
            emailSender, new AuditoriaService(context, new FakeTimeProvider(Agora)), configuracao);
    }

    private static async Task<Usuario> CriarUsuarioAsync(AppDbContext context, string nome, string email, int? setorId = null)
    {
        var usuario = new Usuario
        {
            Nome = nome,
            Email = email,
            DataInicio = new DateOnly(2025, 1, 1),
            SetorId = setorId,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Usuarios.Add(usuario);
        await context.SaveChangesAsync();
        return usuario;
    }

    // Toda campanha agora precisa ter seus itens definidos antes de admitir colaboradores - este
    // helper cobre o fluxo "criar campanha + já deixar a lista de itens pronta" usado pela maioria
    // dos testes, já que o item em si deixou de ser informado por colaborador/lote.
    private static async Task<CampanhaEntregaDto> CriarCampanhaComItensAsync(
        CampanhaEntregaService service, string nome, params (string Descricao, int Quantidade)[] itens)
    {
        var campanha = await service.CreateAsync(new CreateCampanhaEntregaDto { Nome = nome });
        await service.AtualizarItensCampanhaAsync(campanha.Id, new UpdateCampanhaEntregaItensDto
        {
            Itens = itens.Select(i => new CampanhaEntregaItemInputDto { Descricao = i.Descricao, Quantidade = i.Quantidade }).ToList(),
        });
        return campanha;
    }

    [Fact]
    public async Task CreateAsync_DeveCriarComStatusRascunho()
    {
        var service = CriarService(out _);

        var campanha = await service.CreateAsync(new CreateCampanhaEntregaDto { Nome = "Uniformes 2º Semestre" });

        Assert.Equal(CampanhaEntregaStatus.Rascunho, campanha.Status);
    }

    [Fact]
    public async Task AtualizarItensCampanhaAsync_DeveSubstituirListaDeItens()
    {
        var service = CriarService(out _);
        var campanha = await CriarCampanhaComItensAsync(service, "Kit Boas-vindas", ("Mochila", 1));

        var atualizada = await service.AtualizarItensCampanhaAsync(campanha.Id, new UpdateCampanhaEntregaItensDto
        {
            Itens = [new CampanhaEntregaItemInputDto { Descricao = "Garrafa", Quantidade = 2 }],
        });

        Assert.Single(atualizada.Itens);
        Assert.Equal("Garrafa", atualizada.Itens[0].Descricao);
    }

    [Fact]
    public async Task AtualizarItensCampanhaAsync_DevePreencherEntregasPendentesSemItensMasNaoMexerEmQuemJaTemItens()
    {
        var service = CriarService(out var context);
        var campanha = await service.CreateAsync(new CreateCampanhaEntregaDto { Nome = "Kit Boas-vindas" });
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");
        var maria = await CriarUsuarioAsync(context, "Maria", "maria@hope.com");

        // João é adicionado antes de a campanha ter itens (cenário real encontrado em produção) -
        // fica Pendente sem nenhum EntregaItem.
        var entregaJoao = new Entrega
        {
            CampanhaEntregaId = campanha.Id,
            UsuarioId = joao.Id,
            EmailDestino = joao.Email,
            Status = EntregaStatus.Pendente,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Entregas.Add(entregaJoao);

        // Maria já tem item próprio (já foi personalizado manualmente) - não deve ser sobrescrita.
        var entregaMaria = new Entrega
        {
            CampanhaEntregaId = campanha.Id,
            UsuarioId = maria.Id,
            EmailDestino = maria.Email,
            Status = EntregaStatus.Pendente,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
            Itens = [new EntregaItem { Descricao = "Camisa P", Quantidade = 1, DataCriacao = Agora.UtcDateTime }],
        };
        context.Entregas.Add(entregaMaria);
        await context.SaveChangesAsync();

        await service.AtualizarItensCampanhaAsync(campanha.Id, new UpdateCampanhaEntregaItensDto
        {
            Itens = [new CampanhaEntregaItemInputDto { Descricao = "Mochila", Quantidade = 1 }],
        });

        var joaoAtualizado = await service.ObterEntregaAsync(campanha.Id, entregaJoao.Id);
        var mariaAtualizada = await service.ObterEntregaAsync(campanha.Id, entregaMaria.Id);

        Assert.Single(joaoAtualizado.Itens);
        Assert.Equal("Mochila", joaoAtualizado.Itens[0].Descricao);
        Assert.Single(mariaAtualizada.Itens);
        Assert.Equal("Camisa P", mariaAtualizada.Itens[0].Descricao);
    }

    [Fact]
    public async Task AdicionarEntregaAsync_DeveRejeitarQuandoCampanhaNaoTemItens()
    {
        var service = CriarService(out var context);
        var campanha = await service.CreateAsync(new CreateCampanhaEntregaDto { Nome = "Kit Boas-vindas" });
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto { UsuarioId = joao.Id }));
    }

    [Fact]
    public async Task AdicionarEntregaAsync_DeveCopiarItensDaCampanha()
    {
        var service = CriarService(out var context);
        var campanha = await CriarCampanhaComItensAsync(service, "Kit Boas-vindas", ("Mochila", 1), ("Garrafa", 1));
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");

        var entrega = await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto { UsuarioId = joao.Id });

        Assert.Equal(2, entrega.Itens.Count);
        Assert.Contains(entrega.Itens, i => i.Descricao == "Mochila");
        Assert.Contains(entrega.Itens, i => i.Descricao == "Garrafa");
    }

    [Fact]
    public async Task AdicionarEntregaAsync_DevePermitirMaisDeUmaEntregaParaOMesmoColaborador()
    {
        // Diretores/gerentes às vezes pegam kits extras para terceiros em nome próprio -
        // duplicidade deixou de ser bloqueada (ver QuantidadeKits/Observacao para o caso comum).
        var service = CriarService(out var context);
        var campanha = await CriarCampanhaComItensAsync(service, "Kit Boas-vindas", ("Mochila", 1));
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");

        await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto { UsuarioId = joao.Id });
        await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto { UsuarioId = joao.Id, QuantidadeKits = 2, Observacao = "Para clientes" });

        var entregas = await context.Entregas.Where(e => e.CampanhaEntregaId == campanha.Id && e.UsuarioId == joao.Id).ToListAsync();
        Assert.Equal(2, entregas.Count);
    }

    [Fact]
    public async Task AdicionarEntregaAsync_QuantidadeKitsDeveMultiplicarItensCopiadosDaCampanha()
    {
        var service = CriarService(out var context);
        var campanha = await CriarCampanhaComItensAsync(service, "Kit Boas-vindas", ("Mochila", 1), ("Garrafa", 2));
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");

        var entrega = await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto { UsuarioId = joao.Id, QuantidadeKits = 3 });

        Assert.Equal(3, entrega.QuantidadeKits);
        Assert.Equal(3, entrega.Itens.Single(i => i.Descricao == "Mochila").Quantidade);
        Assert.Equal(6, entrega.Itens.Single(i => i.Descricao == "Garrafa").Quantidade);
    }

    [Fact]
    public async Task ObterCampanhaAsync_SaldoDisponivelDeveDescontarEntregasNaoCanceladasEIgnorarCanceladas()
    {
        var service = CriarService(out var context);
        var campanha = await service.CreateAsync(new CreateCampanhaEntregaDto { Nome = "Kit Boas-vindas" });
        await service.AtualizarItensCampanhaAsync(campanha.Id, new UpdateCampanhaEntregaItensDto
        {
            Itens = [new CampanhaEntregaItemInputDto { Descricao = "Mochila", Quantidade = 1, QuantidadeDisponivel = 10 }],
        });
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");
        var maria = await CriarUsuarioAsync(context, "Maria", "maria@hope.com");

        var entregaJoao = await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto { UsuarioId = joao.Id, QuantidadeKits = 2 });
        var entregaMaria = await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto { UsuarioId = maria.Id });

        var entregaMariaEntidade = await context.Entregas.FirstAsync(e => e.Id == entregaMaria.Id);
        entregaMariaEntidade.Status = EntregaStatus.Cancelado;
        await context.SaveChangesAsync();

        var campanhaAtualizada = await service.GetByIdAsync(campanha.Id);
        var itemMochila = campanhaAtualizada.Itens.Single(i => i.Descricao == "Mochila");

        Assert.Equal(2, itemMochila.QuantidadeEntregue);
        Assert.Equal(8, itemMochila.SaldoDisponivel);
    }

    [Fact]
    public async Task AdicionarEntregasLoteAsync_DevePularQuemJaEstaNaCampanhaECopiarItensDaCampanha()
    {
        var service = CriarService(out var context);
        var campanha = await CriarCampanhaComItensAsync(service, "Uniformes", ("Camisa M", 2));
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");
        var maria = await CriarUsuarioAsync(context, "Maria", "maria@hope.com");
        await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto { UsuarioId = joao.Id });

        var resultado = await service.AdicionarEntregasLoteAsync(campanha.Id, new CreateEntregaLoteDto { UsuarioIds = [joao.Id, maria.Id] });

        Assert.Single(resultado);
        Assert.Equal(maria.Id, resultado[0].UsuarioId);
        Assert.Single(resultado[0].Itens);
        Assert.Equal("Camisa M", resultado[0].Itens[0].Descricao);
    }

    [Fact]
    public async Task AtualizarItensEntregaAsync_DeveRejeitarQuandoNaoEstaPendente()
    {
        var service = CriarService(out var context);
        var campanha = await CriarCampanhaComItensAsync(service, "Uniformes", ("Camisa", 1));
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");
        var entrega = await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto { UsuarioId = joao.Id });

        var entregaEntidade = await context.Entregas.FirstAsync(e => e.Id == entrega.Id);
        entregaEntidade.Status = EntregaStatus.EmailEnviado;
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AtualizarItensEntregaAsync(campanha.Id, entrega.Id, new UpdateEntregaItensDto
        {
            Itens = [new CreateEntregaItemDto { Descricao = "Camisa G", Quantidade = 2 }],
        }));
    }

    [Fact]
    public async Task DeleteAsync_DeveRejeitarQuandoCampanhaNaoEstaEmRascunho()
    {
        var service = CriarService(out var context);
        var campanha = await service.CreateAsync(new CreateCampanhaEntregaDto { Nome = "Uniformes" });

        var campanhaEntidade = await context.CampanhasEntrega.FirstAsync(c => c.Id == campanha.Id);
        campanhaEntidade.Status = CampanhaEntregaStatus.EmAndamento;
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.DeleteAsync(campanha.Id));
    }

    [Fact]
    public async Task CancelarAsync_DeveCancelarSoEntregasPendentesOuComEmailEnviado()
    {
        var service = CriarService(out var context);
        var campanha = await CriarCampanhaComItensAsync(service, "Uniformes", ("Camisa", 1));
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");
        var maria = await CriarUsuarioAsync(context, "Maria", "maria@hope.com");
        var entregaJoao = await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto { UsuarioId = joao.Id });
        var entregaMaria = await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto { UsuarioId = maria.Id });

        var entregaMariaEntidade = await context.Entregas.FirstAsync(e => e.Id == entregaMaria.Id);
        entregaMariaEntidade.Status = EntregaStatus.Confirmado;
        await context.SaveChangesAsync();

        await service.CancelarAsync(campanha.Id);

        var joaoAtualizado = await service.ObterEntregaAsync(campanha.Id, entregaJoao.Id);
        var mariaAtualizada = await service.ObterEntregaAsync(campanha.Id, entregaMaria.Id);
        Assert.Equal(EntregaStatus.Cancelado, joaoAtualizado.Status);
        Assert.Equal(EntregaStatus.Confirmado, mariaAtualizada.Status);
    }

    [Fact]
    public async Task ObterResumoAsync_DeveContarPorStatusCorretamente()
    {
        var service = CriarService(out var context);
        var campanha = await CriarCampanhaComItensAsync(service, "Uniformes", ("Camisa", 1));
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");
        var maria = await CriarUsuarioAsync(context, "Maria", "maria@hope.com");
        await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto { UsuarioId = joao.Id });
        var entregaMaria = await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto { UsuarioId = maria.Id });

        var entregaMariaEntidade = await context.Entregas.FirstAsync(e => e.Id == entregaMaria.Id);
        entregaMariaEntidade.Status = EntregaStatus.Confirmado;
        await context.SaveChangesAsync();

        var resumo = await service.ObterResumoAsync(campanha.Id);

        Assert.Equal(2, resumo.Total);
        Assert.Equal(1, resumo.Pendentes);
        Assert.Equal(1, resumo.Confirmados);
    }

    [Fact]
    public async Task ListarColaboradoresDisponiveisAsync_DeveExcluirQuemJaEstaNaCampanha()
    {
        var service = CriarService(out var context);
        var campanha = await CriarCampanhaComItensAsync(service, "Uniformes", ("Camisa", 1));
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");
        var maria = await CriarUsuarioAsync(context, "Maria", "maria@hope.com");
        await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto { UsuarioId = joao.Id });

        var disponiveis = await service.ListarColaboradoresDisponiveisAsync(campanha.Id, new ColaboradorDisponivelFiltroDto());

        Assert.Single(disponiveis);
        Assert.Equal(maria.Id, disponiveis[0].Id);
    }

    [Fact]
    public async Task CancelarEntregaAsync_DeveRejeitarQuandoJaConfirmada()
    {
        var service = CriarService(out var context);
        var campanha = await CriarCampanhaComItensAsync(service, "Uniformes", ("Camisa", 1));
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");
        var entrega = await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto { UsuarioId = joao.Id });

        var entregaEntidade = await context.Entregas.FirstAsync(e => e.Id == entrega.Id);
        entregaEntidade.Status = EntregaStatus.Confirmado;
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CancelarEntregaAsync(campanha.Id, entrega.Id));
    }

    [Fact]
    public async Task RegistrarEntregaFisicaAsync_DeveGravarResponsavelEData()
    {
        var service = CriarService(out var context);
        var campanha = await CriarCampanhaComItensAsync(service, "Uniformes", ("Camisa", 1));
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");
        var rh = await CriarUsuarioAsync(context, "Ana (RH)", "ana@hope.com");
        var entrega = await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto { UsuarioId = joao.Id });

        var atualizada = await service.RegistrarEntregaFisicaAsync(campanha.Id, entrega.Id, new RegistrarEntregaFisicaDto
        {
            DataEntregaFisica = new DateOnly(2026, 9, 14),
            ResponsavelEntregaId = rh.Id,
        });

        Assert.Equal(new DateOnly(2026, 9, 14), atualizada.DataEntregaFisica);
        Assert.Equal("Ana (RH)", atualizada.ResponsavelEntregaNome);
    }

    [Fact]
    public async Task EnviarEmailAsync_DeveGerarTokenEEnviarEmailEAvancarStatus()
    {
        var service = CriarService(out var context, out var emailSender);
        var campanha = await CriarCampanhaComItensAsync(service, "Uniformes", ("Mochila", 1));
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");
        var entrega = await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto { UsuarioId = joao.Id });

        var atualizada = await service.EnviarEmailAsync(campanha.Id, entrega.Id);

        Assert.Equal(EntregaStatus.EmailEnviado, atualizada.Status);
        Assert.NotNull(atualizada.DataEnvioEmail);
        Assert.Null(atualizada.AvisoEmail);
        Assert.Equal(1, emailSender.ChamadasSimples);

        var entregaEntidade = await context.Entregas.FirstAsync(e => e.Id == entrega.Id);
        Assert.NotNull(entregaEntidade.TokenHash);

        var campanhaAtualizada = await service.GetByIdAsync(campanha.Id);
        Assert.Equal(CampanhaEntregaStatus.EmAndamento, campanhaAtualizada.Status);
    }

    [Fact]
    public async Task EnviarEmailAsync_DeveRejeitarSemItens()
    {
        var service = CriarService(out var context);
        var campanha = await service.CreateAsync(new CreateCampanhaEntregaDto { Nome = "Uniformes" });
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");

        var entregaEntidade = new Entrega
        {
            CampanhaEntregaId = campanha.Id,
            UsuarioId = joao.Id,
            EmailDestino = joao.Email,
            Status = EntregaStatus.Pendente,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Entregas.Add(entregaEntidade);
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.EnviarEmailAsync(campanha.Id, entregaEntidade.Id));
    }

    [Fact]
    public async Task EnviarEmailAsync_DeveAvisarSemBloquearQuandoColaboradorNaoTemEmail()
    {
        var service = CriarService(out var context, out var emailSender);
        var campanha = await CriarCampanhaComItensAsync(service, "Uniformes", ("Mochila", 1));
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");
        var entrega = await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto { UsuarioId = joao.Id });

        var entregaEntidade = await context.Entregas.FirstAsync(e => e.Id == entrega.Id);
        entregaEntidade.EmailDestino = "";
        await context.SaveChangesAsync();

        var atualizada = await service.EnviarEmailAsync(campanha.Id, entrega.Id);

        Assert.Equal(EntregaStatus.EmailEnviado, atualizada.Status);
        Assert.NotNull(atualizada.AvisoEmail);
        Assert.Equal(0, emailSender.ChamadasSimples);
    }

    [Fact]
    public async Task ReenviarPendentesAsync_DeveProcessarSoQuemAindaNaoConfirmouENaoMexerEmQuemJaConfirmou()
    {
        var service = CriarService(out var context, out var emailSender);
        var campanha = await CriarCampanhaComItensAsync(service, "Uniformes", ("Mochila", 1));
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");
        var maria = await CriarUsuarioAsync(context, "Maria", "maria@hope.com");
        var entregaJoao = await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto { UsuarioId = joao.Id });
        var entregaMaria = await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto { UsuarioId = maria.Id });

        var entregaMariaEntidade = await context.Entregas.FirstAsync(e => e.Id == entregaMaria.Id);
        entregaMariaEntidade.Status = EntregaStatus.Confirmado;
        await context.SaveChangesAsync();

        var resultados = await service.ReenviarPendentesAsync(campanha.Id);

        Assert.Single(resultados);
        Assert.Equal(joao.Id, resultados[0].UsuarioId);
        Assert.Equal(EntregaStatus.EmailEnviado, resultados[0].Status);
        Assert.Equal(1, emailSender.ChamadasSimples);
    }

    private async Task<(CampanhaEntregaService Service, AppDbContext Context, string Token)> CriarEntregaComEmailEnviadoAsync()
    {
        var service = CriarService(out var context);
        var campanha = await CriarCampanhaComItensAsync(service, "Uniformes", ("Mochila", 1));
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");
        var entrega = await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto { UsuarioId = joao.Id });
        await service.EnviarEmailAsync(campanha.Id, entrega.Id);

        // O token cru nunca é persistido - para simular o link, geramos um token de teste e
        // gravamos apenas o hash dele diretamente na entrega, do mesmo jeito que o serviço faria.
        const string tokenTeste = "token-de-teste-1234567890";
        var entregaEntidade = await context.Entregas.FirstAsync(e => e.Id == entrega.Id);
        entregaEntidade.TokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(tokenTeste)));
        await context.SaveChangesAsync();

        return (service, context, tokenTeste);
    }

    [Fact]
    public async Task ObterPorTokenAsync_DeveLancarNotFoundParaTokenInexistente()
    {
        var service = CriarService(out _);

        await Assert.ThrowsAsync<NotFoundException>(() => service.ObterPorTokenAsync("token-que-nao-existe", "1.2.3.4", "UA"));
    }

    [Fact]
    public async Task ObterPorTokenAsync_DeveGravarAcessoSoNaPrimeiraVez()
    {
        var (service, context, token) = await CriarEntregaComEmailEnviadoAsync();

        await service.ObterPorTokenAsync(token, "1.1.1.1", "UA-1");
        var entregaAposPrimeiro = await context.Entregas.AsNoTracking().FirstAsync();
        await service.ObterPorTokenAsync(token, "2.2.2.2", "UA-2");
        var entregaAposSegundo = await context.Entregas.AsNoTracking().FirstAsync();

        Assert.Equal("1.1.1.1", entregaAposPrimeiro.IpAcessoLink);
        Assert.Equal("1.1.1.1", entregaAposSegundo.IpAcessoLink);
    }

    [Fact]
    public async Task ConfirmarPorTokenAsync_DeveConfirmarEGravarIpEUserAgent()
    {
        var (service, context, token) = await CriarEntregaComEmailEnviadoAsync();

        var resultado = await service.ConfirmarPorTokenAsync(token, "9.9.9.9", "Mozilla/Teste");

        Assert.Equal(EntregaStatus.Confirmado, resultado.Status);
        Assert.NotNull(resultado.DataConfirmacao);

        var entregaEntidade = await context.Entregas.FirstAsync();
        Assert.Equal("9.9.9.9", entregaEntidade.IpConfirmacao);
        Assert.Equal("Mozilla/Teste", entregaEntidade.UserAgentConfirmacao);
    }

    [Fact]
    public async Task ConfirmarPorTokenAsync_DeveRejeitarSegundaConfirmacaoComOMesmoToken()
    {
        var (service, _, token) = await CriarEntregaComEmailEnviadoAsync();
        await service.ConfirmarPorTokenAsync(token, "1.1.1.1", "UA");

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ConfirmarPorTokenAsync(token, "1.1.1.1", "UA"));
    }

    [Fact]
    public async Task RegistrarDivergenciaPorTokenAsync_DeveRegistrarTipoEObservacao()
    {
        var (service, context, token) = await CriarEntregaComEmailEnviadoAsync();

        var resultado = await service.RegistrarDivergenciaPorTokenAsync(
            token, new RegistrarDivergenciaDto { TipoDivergencia = TipoDivergenciaEntrega.ItemDanificado, Observacao = "Veio rasgado" }, "1.1.1.1", "UA");

        Assert.Equal(EntregaStatus.Divergencia, resultado.Status);
        Assert.Equal(TipoDivergenciaEntrega.ItemDanificado, resultado.TipoDivergencia);
        Assert.Equal("Veio rasgado", resultado.ObservacaoDivergencia);
    }

    [Fact]
    public async Task RegistrarDivergenciaPorTokenAsync_DeveRejeitarTipoInvalido()
    {
        var (service, _, token) = await CriarEntregaComEmailEnviadoAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.RegistrarDivergenciaPorTokenAsync(
            token, new RegistrarDivergenciaDto { TipoDivergencia = "TipoQualquerInvalido" }, "1.1.1.1", "UA"));
    }

    [Fact]
    public async Task ListarMinhasEntregasAsync_DeveRetornarSoConfirmadasOuComDivergenciaOrdenadasPorDataDesc()
    {
        var service = CriarService(out var context);
        var campanhaA = await CriarCampanhaComItensAsync(service, "Kit Boas-vindas", ("Mochila", 1));
        var campanhaB = await CriarCampanhaComItensAsync(service, "Uniformes", ("Camisa", 1));
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");

        var entregaPendente = await service.AdicionarEntregaAsync(campanhaA.Id, new CreateEntregaDto { UsuarioId = joao.Id });
        var entregaAntiga = await service.AdicionarEntregaAsync(campanhaB.Id, new CreateEntregaDto { UsuarioId = joao.Id });

        var pendenteEntidade = await context.Entregas.FirstAsync(e => e.Id == entregaPendente.Id);
        pendenteEntidade.Status = EntregaStatus.EmailEnviado;

        var antigaEntidade = await context.Entregas.FirstAsync(e => e.Id == entregaAntiga.Id);
        antigaEntidade.Status = EntregaStatus.Confirmado;
        antigaEntidade.DataConfirmacao = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await context.SaveChangesAsync();

        // Cria uma terceira campanha só pra ter uma confirmação mais recente que a "antiga".
        var campanhaC = await CriarCampanhaComItensAsync(service, "Brindes", ("Caneca", 1));
        var entregaRecente = await service.AdicionarEntregaAsync(campanhaC.Id, new CreateEntregaDto { UsuarioId = joao.Id });
        var recenteEntidade = await context.Entregas.FirstAsync(e => e.Id == entregaRecente.Id);
        recenteEntidade.Status = EntregaStatus.Confirmado;
        recenteEntidade.DataConfirmacao = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        await context.SaveChangesAsync();

        var historico = await service.ListarMinhasEntregasAsync(joao.Id);

        Assert.Equal(2, historico.Count);
        Assert.Equal("Brindes", historico[0].CampanhaNome);
        Assert.Equal("Uniformes", historico[1].CampanhaNome);
    }
}

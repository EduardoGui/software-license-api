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

    [Fact]
    public async Task CreateAsync_DeveCriarComStatusRascunho()
    {
        var service = CriarService(out _);

        var campanha = await service.CreateAsync(new CreateCampanhaEntregaDto { Nome = "Uniformes 2º Semestre" });

        Assert.Equal(CampanhaEntregaStatus.Rascunho, campanha.Status);
    }

    [Fact]
    public async Task AdicionarEntregaAsync_DeveRejeitarColaboradorDuplicadoNaMesmaCampanha()
    {
        var service = CriarService(out var context);
        var campanha = await service.CreateAsync(new CreateCampanhaEntregaDto { Nome = "Kit Boas-vindas" });
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");

        await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto
        {
            UsuarioId = joao.Id,
            Itens = [new CreateEntregaItemDto { Descricao = "Mochila", Quantidade = 1 }],
        });

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto
        {
            UsuarioId = joao.Id,
            Itens = [new CreateEntregaItemDto { Descricao = "Garrafa", Quantidade = 1 }],
        }));
    }

    [Fact]
    public async Task AdicionarEntregasLoteAsync_DevePularQuemJaEstaNaCampanha()
    {
        var service = CriarService(out var context);
        var campanha = await service.CreateAsync(new CreateCampanhaEntregaDto { Nome = "Uniformes" });
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");
        var maria = await CriarUsuarioAsync(context, "Maria", "maria@hope.com");
        await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto
        {
            UsuarioId = joao.Id,
            Itens = [new CreateEntregaItemDto { Descricao = "Camisa", Quantidade = 1 }],
        });

        var resultado = await service.AdicionarEntregasLoteAsync(campanha.Id, new CreateEntregaLoteDto
        {
            UsuarioIds = [joao.Id, maria.Id],
            ItensPadrao = [new CreateEntregaItemDto { Descricao = "Camisa M", Quantidade = 2 }],
        });

        Assert.Single(resultado);
        Assert.Equal(maria.Id, resultado[0].UsuarioId);
    }

    [Fact]
    public async Task AtualizarItensEntregaAsync_DeveRejeitarQuandoNaoEstaPendente()
    {
        var service = CriarService(out var context);
        var campanha = await service.CreateAsync(new CreateCampanhaEntregaDto { Nome = "Uniformes" });
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");
        var entrega = await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto
        {
            UsuarioId = joao.Id,
            Itens = [new CreateEntregaItemDto { Descricao = "Camisa", Quantidade = 1 }],
        });

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
        var campanha = await service.CreateAsync(new CreateCampanhaEntregaDto { Nome = "Uniformes" });
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");
        var maria = await CriarUsuarioAsync(context, "Maria", "maria@hope.com");
        var entregaJoao = await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto
        {
            UsuarioId = joao.Id,
            Itens = [new CreateEntregaItemDto { Descricao = "Camisa", Quantidade = 1 }],
        });
        var entregaMaria = await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto
        {
            UsuarioId = maria.Id,
            Itens = [new CreateEntregaItemDto { Descricao = "Camisa", Quantidade = 1 }],
        });

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
        var campanha = await service.CreateAsync(new CreateCampanhaEntregaDto { Nome = "Uniformes" });
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");
        var maria = await CriarUsuarioAsync(context, "Maria", "maria@hope.com");
        await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto { UsuarioId = joao.Id, Itens = [new CreateEntregaItemDto { Descricao = "Camisa", Quantidade = 1 }] });
        var entregaMaria = await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto { UsuarioId = maria.Id, Itens = [new CreateEntregaItemDto { Descricao = "Camisa", Quantidade = 1 }] });

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
        var campanha = await service.CreateAsync(new CreateCampanhaEntregaDto { Nome = "Uniformes" });
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");
        var maria = await CriarUsuarioAsync(context, "Maria", "maria@hope.com");
        await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto { UsuarioId = joao.Id, Itens = [new CreateEntregaItemDto { Descricao = "Camisa", Quantidade = 1 }] });

        var disponiveis = await service.ListarColaboradoresDisponiveisAsync(campanha.Id, new ColaboradorDisponivelFiltroDto());

        Assert.Single(disponiveis);
        Assert.Equal(maria.Id, disponiveis[0].Id);
    }

    [Fact]
    public async Task CancelarEntregaAsync_DeveRejeitarQuandoJaConfirmada()
    {
        var service = CriarService(out var context);
        var campanha = await service.CreateAsync(new CreateCampanhaEntregaDto { Nome = "Uniformes" });
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");
        var entrega = await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto { UsuarioId = joao.Id, Itens = [new CreateEntregaItemDto { Descricao = "Camisa", Quantidade = 1 }] });

        var entregaEntidade = await context.Entregas.FirstAsync(e => e.Id == entrega.Id);
        entregaEntidade.Status = EntregaStatus.Confirmado;
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CancelarEntregaAsync(campanha.Id, entrega.Id));
    }

    [Fact]
    public async Task RegistrarEntregaFisicaAsync_DeveGravarResponsavelEData()
    {
        var service = CriarService(out var context);
        var campanha = await service.CreateAsync(new CreateCampanhaEntregaDto { Nome = "Uniformes" });
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");
        var rh = await CriarUsuarioAsync(context, "Ana (RH)", "ana@hope.com");
        var entrega = await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto { UsuarioId = joao.Id, Itens = [new CreateEntregaItemDto { Descricao = "Camisa", Quantidade = 1 }] });

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
        var campanha = await service.CreateAsync(new CreateCampanhaEntregaDto { Nome = "Uniformes" });
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");
        var entrega = await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto
        {
            UsuarioId = joao.Id,
            Itens = [new CreateEntregaItemDto { Descricao = "Mochila", Quantidade = 1 }],
        });

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
        var campanha = await service.CreateAsync(new CreateCampanhaEntregaDto { Nome = "Uniformes" });
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");
        var entrega = await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto
        {
            UsuarioId = joao.Id,
            Itens = [new CreateEntregaItemDto { Descricao = "Mochila", Quantidade = 1 }],
        });

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
        var campanha = await service.CreateAsync(new CreateCampanhaEntregaDto { Nome = "Uniformes" });
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");
        var maria = await CriarUsuarioAsync(context, "Maria", "maria@hope.com");
        var entregaJoao = await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto { UsuarioId = joao.Id, Itens = [new CreateEntregaItemDto { Descricao = "Mochila", Quantidade = 1 }] });
        var entregaMaria = await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto { UsuarioId = maria.Id, Itens = [new CreateEntregaItemDto { Descricao = "Mochila", Quantidade = 1 }] });

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
        var campanha = await service.CreateAsync(new CreateCampanhaEntregaDto { Nome = "Uniformes" });
        var joao = await CriarUsuarioAsync(context, "João", "joao@hope.com");
        var entrega = await service.AdicionarEntregaAsync(campanha.Id, new CreateEntregaDto
        {
            UsuarioId = joao.Id,
            Itens = [new CreateEntregaItemDto { Descricao = "Mochila", Quantidade = 1 }],
        });
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
}

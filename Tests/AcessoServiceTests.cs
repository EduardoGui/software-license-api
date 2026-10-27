using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class AcessoServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);

    private class EmailSenderQuebrado : IEmailSender
    {
        public Task EnviarAsync(string destinatario, string assunto, string corpoHtml) =>
            throw new InvalidOperationException("falha simulada");

        public Task EnviarAsync(
            IReadOnlyList<string> destinatarios, string assunto, string corpoHtml,
            IReadOnlyList<string>? copia = null, IReadOnlyList<EmailAnexo>? anexos = null) =>
            throw new InvalidOperationException("falha simulada");
    }

    private class Ambiente
    {
        public required AppDbContext Context { get; init; }
        public required UserManager<ApplicationUser> UserManager { get; init; }
        public required AcessoService Acesso { get; init; }
        public required UsuarioService Usuarios { get; init; }
        public required ContaAcessoValidador Validador { get; init; }
        public required AuthService Auth { get; init; }
        public required FakeEmailSender Emails { get; init; }
    }

    private static Ambiente Criar(IEmailSender? emailSender = null)
    {
        var emails = new FakeEmailSender();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new AppDbContext(options);

        var services = new ServiceCollection();
        services.AddSingleton(context);
        services.AddLogging();
        services.AddDataProtection();
        services.AddIdentityCore<ApplicationUser>(o => AuthService.ConfigurarBloqueioPorTentativas(o.Lockout))
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();
        var provider = services.BuildServiceProvider();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var papel in new[] { Roles.Administrador, Roles.Colaborador, Roles.Administrativo })
        {
            roleManager.CreateAsync(new IdentityRole(papel)).GetAwaiter().GetResult();
        }

        var tempo = new FakeTimeProvider(Agora);
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "SegredoDeTesteBemGrandeParaAssinaturaHmacSha256Funcionar1234567890",
            })
            .Build();
        var validador = new ContaAcessoValidador(context, tempo);
        var auditoria = new AuditoriaService(context, tempo);

        return new Ambiente
        {
            Context = context,
            UserManager = userManager,
            Validador = validador,
            Acesso = new AcessoService(context, userManager, auditoria, tempo, NullLogger<AcessoService>.Instance),
            Usuarios = new UsuarioService(
                context, tempo, NullLogger<UsuarioService>.Instance, userManager,
                emailSender ?? emails, auditoria, configuracao),
            Auth = new AuthService(userManager, validador, emails, auditoria, tempo, configuracao, NullLogger<AuthService>.Instance),
            Emails = emails,
        };
    }

    private static CreateUsuarioDto NovoColaborador(string email = "ana@empresa.com") =>
        new() { Nome = "Ana", Email = email, DataInicio = new DateOnly(2026, 1, 1) };

    private static async Task<ApplicationUser> ContaDe(Ambiente a, int usuarioId) =>
        await a.UserManager.Users.SingleAsync(u => u.UsuarioId == usuarioId);

    private static async Task DefinirSenha(Ambiente a, ApplicationUser conta, string senha)
    {
        var token = await a.UserManager.GeneratePasswordResetTokenAsync(conta);
        await a.UserManager.ResetPasswordAsync(conta, token, senha);
    }

    // ----- login / validador -----

    [Fact]
    public async Task Login_DeveSerRecusadoParaColaboradorDesativado()
    {
        var a = Criar();
        var criado = await a.Usuarios.CreateAsync(NovoColaborador());
        await DefinirSenha(a, await ContaDe(a, criado.Id), "Senha@Forte123");
        Assert.NotNull(await a.Auth.LoginAsync(new LoginDto { Email = "ana@empresa.com", Senha = "Senha@Forte123" }));

        await a.Usuarios.DesativarAsync(criado.Id, new DesativarUsuarioDto { DataFim = new DateOnly(2026, 8, 12) });

        Assert.Null(await a.Auth.LoginAsync(new LoginDto { Email = "ana@empresa.com", Senha = "Senha@Forte123" }));
    }

    [Fact]
    public async Task Login_DeveVoltarAFuncionarDepoisDeReativar()
    {
        var a = Criar();
        var criado = await a.Usuarios.CreateAsync(NovoColaborador());
        await DefinirSenha(a, await ContaDe(a, criado.Id), "Senha@Forte123");
        await a.Usuarios.DesativarAsync(criado.Id, new DesativarUsuarioDto { DataFim = new DateOnly(2026, 8, 12) });

        await a.Usuarios.ReativarAsync(criado.Id);

        Assert.NotNull(await a.Auth.LoginAsync(new LoginDto { Email = "ana@empresa.com", Senha = "Senha@Forte123" }));
    }

    [Fact]
    public async Task Validador_DevePermitirColaboradorComDesligamentoFuturo()
    {
        var a = Criar();
        var criado = await a.Usuarios.CreateAsync(NovoColaborador());
        await a.Usuarios.DesativarAsync(criado.Id, new DesativarUsuarioDto { DataFim = new DateOnly(2026, 8, 13) });

        Assert.True(await a.Validador.PodeAcessarAsync((await ContaDe(a, criado.Id)).Id));
    }

    [Fact]
    public async Task Validador_DevePermitirColaboradorAgendado()
    {
        var a = Criar();
        var criado = await a.Usuarios.CreateAsync(
            new CreateUsuarioDto { Nome = "Novo", Email = "novo@empresa.com", DataInicio = new DateOnly(2026, 9, 1) });

        Assert.True(await a.Validador.PodeAcessarAsync((await ContaDe(a, criado.Id)).Id));
    }

    [Fact]
    public async Task Validador_DevePermitirContaSemColaborador_ERecusarContaInexistente()
    {
        var a = Criar();
        var admin = new ApplicationUser { UserName = "admin@licencas.local", Email = "admin@licencas.local" };
        await a.UserManager.CreateAsync(admin, "Senha@Forte123");

        Assert.True(await a.Validador.PodeAcessarAsync(admin.Id));
        Assert.False(await a.Validador.PodeAcessarAsync("nao-existe"));
    }

    // ----- bloquear / desbloquear -----

    [Fact]
    public async Task Bloquear_DeveImpedirLogoEDesbloquearDeveLiberar()
    {
        var a = Criar();
        var criado = await a.Usuarios.CreateAsync(NovoColaborador());
        var conta = await ContaDe(a, criado.Id);
        await DefinirSenha(a, conta, "Senha@Forte123");

        await a.Acesso.BloquearAsync(conta.Id, contaIdSolicitante: "outra-conta");

        Assert.False(await a.Validador.PodeAcessarAsync(conta.Id));
        Assert.Null(await a.Auth.LoginAsync(new LoginDto { Email = "ana@empresa.com", Senha = "Senha@Forte123" }));

        await a.Acesso.DesbloquearAsync(conta.Id);

        Assert.True(await a.Validador.PodeAcessarAsync(conta.Id));
        Assert.NotNull(await a.Auth.LoginAsync(new LoginDto { Email = "ana@empresa.com", Senha = "Senha@Forte123" }));
    }

    [Fact]
    public async Task Bloquear_DeveRejeitarOPropriOAcesso()
    {
        var a = Criar();
        var criado = await a.Usuarios.CreateAsync(NovoColaborador());
        var conta = await ContaDe(a, criado.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => a.Acesso.BloquearAsync(conta.Id, conta.Id));
    }

    // ----- papéis -----

    [Fact]
    public async Task DefinirPapeis_DeveAdicionarAdministrativoMantendoColaborador()
    {
        var a = Criar();
        var criado = await a.Usuarios.CreateAsync(NovoColaborador());
        var conta = await ContaDe(a, criado.Id);

        await a.Acesso.DefinirPapeisAsync(conta.Id, [Roles.Administrativo], contaIdSolicitante: "admin");

        var papeis = await a.UserManager.GetRolesAsync(conta);
        Assert.Equal(new[] { Roles.Administrativo, Roles.Colaborador }.Order(), papeis.Order());
    }

    [Fact]
    public async Task DefinirPapeis_DeveRejeitarPapelInvalido()
    {
        var a = Criar();
        var criado = await a.Usuarios.CreateAsync(NovoColaborador());
        var conta = await ContaDe(a, criado.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            a.Acesso.DefinirPapeisAsync(conta.Id, ["SuperUsuario"], "admin"));
    }

    [Fact]
    public async Task DefinirPapeis_DeveRejeitarRemoverOPropriOPapelDeAdministrador()
    {
        var a = Criar();
        var criado = await a.Usuarios.CreateAsync(NovoColaborador());
        var conta = await ContaDe(a, criado.Id);
        await a.Acesso.DefinirPapeisAsync(conta.Id, [Roles.Administrador], "outro");
        var outroAdmin = new ApplicationUser { UserName = "admin@licencas.local", Email = "admin@licencas.local" };
        await a.UserManager.CreateAsync(outroAdmin, "Senha@Forte123");
        await a.UserManager.AddToRoleAsync(outroAdmin, Roles.Administrador);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            a.Acesso.DefinirPapeisAsync(conta.Id, [Roles.Colaborador], contaIdSolicitante: conta.Id));
    }

    [Fact]
    public async Task DefinirPapeis_DeveRejeitarRemoverOUltimoAdministrador()
    {
        var a = Criar();
        var criado = await a.Usuarios.CreateAsync(NovoColaborador());
        var conta = await ContaDe(a, criado.Id);
        await a.Acesso.DefinirPapeisAsync(conta.Id, [Roles.Administrador], "outro");

        var erro = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            a.Acesso.DefinirPapeisAsync(conta.Id, [Roles.Colaborador], contaIdSolicitante: "outra-conta"));
        Assert.Contains("ao menos um Administrador", erro.Message);
    }

    [Fact]
    public async Task DefinirPapeis_DevePermitirRemoverAdministradorQuandoHaOutro()
    {
        var a = Criar();
        var criado = await a.Usuarios.CreateAsync(NovoColaborador());
        var conta = await ContaDe(a, criado.Id);
        await a.Acesso.DefinirPapeisAsync(conta.Id, [Roles.Administrador], "outro");
        var outroAdmin = new ApplicationUser { UserName = "admin@licencas.local", Email = "admin@licencas.local" };
        await a.UserManager.CreateAsync(outroAdmin, "Senha@Forte123");
        await a.UserManager.AddToRoleAsync(outroAdmin, Roles.Administrador);

        await a.Acesso.DefinirPapeisAsync(conta.Id, [Roles.Colaborador], contaIdSolicitante: outroAdmin.Id);

        Assert.Equal([Roles.Colaborador], await a.UserManager.GetRolesAsync(conta));
    }

    // ----- listagem -----

    [Fact]
    public async Task Listar_DeveTrazerColaboradorSemConta_ComConta_ContaSemColaborador()
    {
        var a = Criar();
        var comConta = await a.Usuarios.CreateAsync(NovoColaborador("ana@empresa.com"));
        a.Context.Usuarios.Add(new Usuario
        {
            Nome = "Sem Conta", Email = "sem@empresa.com", DataInicio = new DateOnly(2026, 1, 1),
            DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime,
        });
        await a.Context.SaveChangesAsync();
        var admin = new ApplicationUser { UserName = "admin@licencas.local", Email = "admin@licencas.local" };
        await a.UserManager.CreateAsync(admin, "Senha@Forte123");
        await a.UserManager.AddToRoleAsync(admin, Roles.Administrador);
        await a.Acesso.BloquearAsync((await ContaDe(a, comConta.Id)).Id, admin.Id);

        var linhas = await a.Acesso.ListarAsync(admin.Id);

        var ana = linhas.Single(l => l.Nome == "Ana");
        Assert.True(ana.TemConta);
        Assert.True(ana.Bloqueada);
        Assert.Equal([Roles.Colaborador], ana.Papeis);
        Assert.Equal("Ativo", ana.StatusUsuario);

        var semConta = linhas.Single(l => l.Nome == "Sem Conta");
        Assert.False(semConta.TemConta);
        Assert.Null(semConta.ContaId);

        var orfa = linhas.Single(l => l.UsuarioId is null);
        Assert.Equal("admin@licencas.local", orfa.Email);
        Assert.True(orfa.EhVoce);
        Assert.Equal([Roles.Administrador], orfa.Papeis);
    }

    // ----- UsuarioService: reativar, e-mail da conta, conta de acesso, convite -----

    [Fact]
    public async Task Reativar_DeveLimparDataFim_ERejeitarQuemNaoEstaDesativado()
    {
        var a = Criar();
        var criado = await a.Usuarios.CreateAsync(NovoColaborador());

        await Assert.ThrowsAsync<BusinessRuleException>(() => a.Usuarios.ReativarAsync(criado.Id));

        await a.Usuarios.DesativarAsync(criado.Id, new DesativarUsuarioDto { DataFim = new DateOnly(2026, 8, 12) });
        var reativado = await a.Usuarios.ReativarAsync(criado.Id);

        Assert.Null(reativado.DataFim);
        Assert.Equal("Ativo", reativado.Status);
    }

    [Fact]
    public async Task Update_DeveAcompanharOEmailDaContaDeAcesso()
    {
        var a = Criar();
        var criado = await a.Usuarios.CreateAsync(NovoColaborador("ana@empresa.com"));

        await a.Usuarios.UpdateAsync(criado.Id, new UpdateUsuarioDto
        {
            Nome = "Ana", Email = "ana.nova@empresa.com", DataInicio = new DateOnly(2026, 1, 1),
        });

        var conta = await ContaDe(a, criado.Id);
        Assert.Equal("ana.nova@empresa.com", conta.Email);
        Assert.Equal("ana.nova@empresa.com", conta.UserName);
        Assert.Null(await a.UserManager.FindByEmailAsync("ana@empresa.com"));
    }

    [Fact]
    public async Task Update_DeveRejeitarEmailQueJaPertenceAOutraConta()
    {
        var a = Criar();
        var ana = await a.Usuarios.CreateAsync(NovoColaborador("ana@empresa.com"));
        await a.UserManager.CreateAsync(new ApplicationUser { UserName = "livre@empresa.com", Email = "livre@empresa.com" }, "Senha@Forte123");

        await Assert.ThrowsAsync<BusinessRuleException>(() => a.Usuarios.UpdateAsync(ana.Id, new UpdateUsuarioDto
        {
            Nome = "Ana", Email = "livre@empresa.com", DataInicio = new DateOnly(2026, 1, 1),
        }));
    }

    [Fact]
    public async Task Create_DeveSinalizarQuandoOConviteFalha()
    {
        var comSucesso = await Criar().Usuarios.CreateAsync(NovoColaborador());
        Assert.True(comSucesso.ConviteEnviado);

        var a = Criar(new EmailSenderQuebrado());
        var comFalha = await a.Usuarios.CreateAsync(NovoColaborador());

        Assert.False(comFalha.ConviteEnviado);
        Assert.NotNull(await ContaDe(a, comFalha.Id));
    }

    [Fact]
    public async Task CriarContaAcesso_DeveCriarContaColaboradorParaQuemNaoTem()
    {
        var a = Criar();
        a.Context.Usuarios.Add(new Usuario
        {
            Nome = "Sem Conta", Email = "sem@empresa.com", DataInicio = new DateOnly(2026, 1, 1),
            DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime,
        });
        await a.Context.SaveChangesAsync();
        var id = (await a.Context.Usuarios.SingleAsync()).Id;

        var convite = await a.Usuarios.CriarContaAcessoAsync(id);

        Assert.True(convite);
        Assert.Equal([Roles.Colaborador], await a.UserManager.GetRolesAsync(await ContaDe(a, id)));
        await Assert.ThrowsAsync<BusinessRuleException>(() => a.Usuarios.CriarContaAcessoAsync(id));
    }

    // ----- esqueci minha senha -----

    [Fact]
    public async Task EsqueciSenha_DeveEnviarLinkParaContaQuePodeEntrar()
    {
        var a = Criar();
        await a.Usuarios.CreateAsync(NovoColaborador());
        var antes = a.Emails.ChamadasSimples;

        await a.Auth.EsqueciSenhaAsync(new EsqueciSenhaDto { Email = "ana@empresa.com" });

        Assert.Equal(antes + 1, a.Emails.ChamadasSimples);
        Assert.Equal("ana@empresa.com", a.Emails.UltimoDestinatarioSimples);
        Assert.Contains("redefinição de senha", a.Emails.UltimoAssuntoSimples);
        Assert.Contains("/definir-senha?email=ana%40empresa.com&token=", a.Emails.UltimoCorpoSimples);
    }

    [Fact]
    public async Task EsqueciSenha_NaoDeveEnviarParaEmailInexistente_NemLancarErro()
    {
        var a = Criar();

        await a.Auth.EsqueciSenhaAsync(new EsqueciSenhaDto { Email = "ninguem@empresa.com" });

        Assert.Equal(0, a.Emails.ChamadasSimples);
    }

    [Fact]
    public async Task EsqueciSenha_NaoDeveEnviarParaContaBloqueadaOuDesligada()
    {
        var a = Criar();
        var bloqueada = await a.Usuarios.CreateAsync(NovoColaborador("bloqueada@empresa.com"));
        var desligada = await a.Usuarios.CreateAsync(NovoColaborador("desligada@empresa.com"));
        await a.Acesso.BloquearAsync((await ContaDe(a, bloqueada.Id)).Id, "admin");
        await a.Usuarios.DesativarAsync(desligada.Id, new DesativarUsuarioDto { DataFim = new DateOnly(2026, 8, 12) });
        var antes = a.Emails.ChamadasSimples;

        await a.Auth.EsqueciSenhaAsync(new EsqueciSenhaDto { Email = "bloqueada@empresa.com" });
        await a.Auth.EsqueciSenhaAsync(new EsqueciSenhaDto { Email = "desligada@empresa.com" });

        Assert.Equal(antes, a.Emails.ChamadasSimples);
    }

    [Fact]
    public async Task EsqueciSenha_DeveEngolirFalhaNoEnvioDoEmail()
    {
        var a = Criar(new EmailSenderQuebrado());
        await a.Usuarios.CreateAsync(NovoColaborador());

        await a.Auth.EsqueciSenhaAsync(new EsqueciSenhaDto { Email = "ana@empresa.com" });
    }

    [Fact]
    public async Task DefinirSenha_NaoDeveLiberarContaBloqueadaPeloAdministrador()
    {
        var a = Criar();
        var criado = await a.Usuarios.CreateAsync(NovoColaborador());
        var conta = await ContaDe(a, criado.Id);
        await a.Acesso.BloquearAsync(conta.Id, "admin");

        await DefinirSenha(a, conta, "Senha@Forte123");

        Assert.False(await a.Validador.PodeAcessarAsync(conta.Id));
        Assert.Null(await a.Auth.LoginAsync(new LoginDto { Email = "ana@empresa.com", Senha = "Senha@Forte123" }));
    }

    // ----- alterar a própria senha -----

    [Fact]
    public async Task AlterarSenha_DeveTrocarERegistrarNaAuditoria()
    {
        var a = Criar();
        var criado = await a.Usuarios.CreateAsync(NovoColaborador());
        var conta = await ContaDe(a, criado.Id);
        await DefinirSenha(a, conta, "Senha@Forte123");

        await a.Auth.AlterarSenhaAsync(conta.Id, new AlterarSenhaDto { SenhaAtual = "Senha@Forte123", NovaSenha = "Outra@Senha456" });

        Assert.Null(await a.Auth.LoginAsync(new LoginDto { Email = "ana@empresa.com", Senha = "Senha@Forte123" }));
        Assert.NotNull(await a.Auth.LoginAsync(new LoginDto { Email = "ana@empresa.com", Senha = "Outra@Senha456" }));
        Assert.Single(a.Context.LogsAuditoria.Where(l => l.Acao == LogAuditoriaAcao.SenhaAlterada && l.EntidadeId == criado.Id));
    }

    [Fact]
    public async Task AlterarSenha_DeveRejeitarSenhaAtualErrada_ESenhaFraca()
    {
        var a = Criar();
        var criado = await a.Usuarios.CreateAsync(NovoColaborador());
        var conta = await ContaDe(a, criado.Id);
        await DefinirSenha(a, conta, "Senha@Forte123");

        var erro = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            a.Auth.AlterarSenhaAsync(conta.Id, new AlterarSenhaDto { SenhaAtual = "errada", NovaSenha = "Outra@Senha456" }));
        Assert.Equal("Senha atual incorreta.", erro.Message);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            a.Auth.AlterarSenhaAsync(conta.Id, new AlterarSenhaDto { SenhaAtual = "Senha@Forte123", NovaSenha = "123" }));
        Assert.NotNull(await a.Auth.LoginAsync(new LoginDto { Email = "ana@empresa.com", Senha = "Senha@Forte123" }));
    }

    // ----- bloqueio por tentativas -----

    private static async Task ErrarSenha(Ambiente a, int vezes)
    {
        for (var i = 0; i < vezes; i++)
        {
            Assert.Null(await a.Auth.LoginAsync(new LoginDto { Email = "ana@empresa.com", Senha = "errada" }));
        }
    }

    [Fact]
    public async Task Login_DeveBloquearAposDezSenhasErradas_MesmoComASenhaCertaDepois()
    {
        var a = Criar();
        var criado = await a.Usuarios.CreateAsync(NovoColaborador());
        await DefinirSenha(a, await ContaDe(a, criado.Id), "Senha@Forte123");

        await ErrarSenha(a, 10);

        Assert.Null(await a.Auth.LoginAsync(new LoginDto { Email = "ana@empresa.com", Senha = "Senha@Forte123" }));
        var linha = (await a.Acesso.ListarAsync(null)).Single(l => l.UsuarioId == criado.Id);
        Assert.True(linha.Bloqueada);
        Assert.True(linha.BloqueioTemporario);
        Assert.Single(a.Context.LogsAuditoria.Where(l => l.Acao == LogAuditoriaAcao.BloqueioPorTentativas && l.EntidadeId == criado.Id));
    }

    [Fact]
    public async Task Login_NaoDeveBloquearComNoveErros_ESucessoZeraOContador()
    {
        var a = Criar();
        var criado = await a.Usuarios.CreateAsync(NovoColaborador());
        await DefinirSenha(a, await ContaDe(a, criado.Id), "Senha@Forte123");

        await ErrarSenha(a, 9);
        Assert.NotNull(await a.Auth.LoginAsync(new LoginDto { Email = "ana@empresa.com", Senha = "Senha@Forte123" }));
        await ErrarSenha(a, 9);

        Assert.NotNull(await a.Auth.LoginAsync(new LoginDto { Email = "ana@empresa.com", Senha = "Senha@Forte123" }));
    }

    [Fact]
    public async Task Desbloquear_DeveLiberarContaTravadaPorTentativas()
    {
        var a = Criar();
        var criado = await a.Usuarios.CreateAsync(NovoColaborador());
        var conta = await ContaDe(a, criado.Id);
        await DefinirSenha(a, conta, "Senha@Forte123");
        await ErrarSenha(a, 10);

        await a.Acesso.DesbloquearAsync(conta.Id);

        Assert.NotNull(await a.Auth.LoginAsync(new LoginDto { Email = "ana@empresa.com", Senha = "Senha@Forte123" }));
    }

    [Fact]
    public async Task Listar_BloqueioDoAdministradorNaoEhTemporario()
    {
        var a = Criar();
        var criado = await a.Usuarios.CreateAsync(NovoColaborador());
        await a.Acesso.BloquearAsync((await ContaDe(a, criado.Id)).Id, "admin");

        var linha = (await a.Acesso.ListarAsync(null)).Single(l => l.UsuarioId == criado.Id);

        Assert.True(linha.Bloqueada);
        Assert.False(linha.BloqueioTemporario);
    }

    // ----- último acesso -----

    [Fact]
    public async Task Login_DeveGravarUltimoAcesso_AntesDissoEhNulo()
    {
        var a = Criar();
        var criado = await a.Usuarios.CreateAsync(NovoColaborador());
        await DefinirSenha(a, await ContaDe(a, criado.Id), "Senha@Forte123");
        Assert.Null((await a.Acesso.ListarAsync(null)).Single(l => l.UsuarioId == criado.Id).UltimoAcessoEm);

        await a.Auth.LoginAsync(new LoginDto { Email = "ana@empresa.com", Senha = "Senha@Forte123" });

        Assert.Equal(Agora.UtcDateTime, (await a.Acesso.ListarAsync(null)).Single(l => l.UsuarioId == criado.Id).UltimoAcessoEm);
    }

    // ----- auditoria dos acessos -----

    [Fact]
    public async Task Acoes_DevemFicarNaAuditoriaComOAtor()
    {
        var a = Criar();
        var ator = await a.Usuarios.CreateAsync(NovoColaborador("admin.pessoa@empresa.com"));
        var criado = await a.Usuarios.CreateAsync(NovoColaborador(), ator.Id);
        var conta = await ContaDe(a, criado.Id);

        await a.Acesso.DefinirPapeisAsync(conta.Id, [Roles.Administrativo], "admin", ator.Id);
        await a.Acesso.BloquearAsync(conta.Id, "admin", ator.Id);
        await a.Acesso.DesbloquearAsync(conta.Id, ator.Id);
        await a.Usuarios.ReenviarConviteAsync(criado.Id, ator.Id);
        await a.Usuarios.DesativarAsync(criado.Id, new DesativarUsuarioDto { DataFim = new DateOnly(2026, 8, 12) }, ator.Id);
        await a.Usuarios.ReativarAsync(criado.Id, ator.Id);

        var logs = a.Context.LogsAuditoria
            .Where(l => l.Entidade == LogAuditoriaEntidade.AcessoUsuario && l.EntidadeId == criado.Id)
            .ToList();
        Assert.Equal(
            new[]
            {
                LogAuditoriaAcao.ConviteReenviado, LogAuditoriaAcao.ContaBloqueada, LogAuditoriaAcao.ContaCriada,
                LogAuditoriaAcao.ContaLiberada, LogAuditoriaAcao.PapeisAlterados, LogAuditoriaAcao.UsuarioDesativado,
                LogAuditoriaAcao.UsuarioReativado,
            }.Order(),
            logs.Select(l => l.Acao).Order());
        Assert.All(logs, l => Assert.Equal(ator.Id, l.UsuarioId));
        Assert.Contains(logs, l => l.Acao == LogAuditoriaAcao.PapeisAlterados && l.Detalhe!.Contains("adicionado(s): Administrativo"));
    }

    [Fact]
    public async Task DefinirSenha_DeveFicarNaAuditoria()
    {
        var a = Criar();
        var criado = await a.Usuarios.CreateAsync(NovoColaborador());
        var conta = await ContaDe(a, criado.Id);
        var token = await a.UserManager.GeneratePasswordResetTokenAsync(conta);

        await a.Auth.DefinirSenhaAsync(new DefinirSenhaDto { Email = "ana@empresa.com", Token = token, NovaSenha = "Senha@Forte123" });

        Assert.Single(a.Context.LogsAuditoria.Where(l => l.Acao == LogAuditoriaAcao.SenhaDefinida && l.EntidadeId == criado.Id));
    }
}

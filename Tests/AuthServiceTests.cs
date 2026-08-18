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

public class AuthServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);

    private static (AuthService Service, UserManager<ApplicationUser> UserManager) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new AppDbContext(options);

        var services = new ServiceCollection();
        services.AddSingleton(context);
        services.AddLogging();
        services.AddDataProtection();
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();
        var provider = services.BuildServiceProvider();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();

        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "SegredoDeTesteBemGrandeParaAssinaturaHmacSha256Funcionar1234567890",
                ["Jwt:Issuer"] = "TesteIssuer",
            })
            .Build();

        var service = new AuthService(userManager, new FakeTimeProvider(Agora), configuracao, NullLogger<AuthService>.Instance);
        return (service, userManager);
    }

    [Fact]
    public async Task LoginAsync_DeveRetornarTokenParaCredenciaisValidas()
    {
        var (service, userManager) = CriarService();
        await userManager.CreateAsync(new ApplicationUser { UserName = "admin@licencas.local", Email = "admin@licencas.local" }, "Senha@Forte123");

        var resultado = await service.LoginAsync(new LoginDto { Email = "admin@licencas.local", Senha = "Senha@Forte123" });

        Assert.NotNull(resultado);
        Assert.False(string.IsNullOrWhiteSpace(resultado!.Token));
        Assert.Equal("admin@licencas.local", resultado.Email);
    }

    [Fact]
    public async Task LoginAsync_DeveRetornarNuloParaSenhaIncorreta()
    {
        var (service, userManager) = CriarService();
        await userManager.CreateAsync(new ApplicationUser { UserName = "admin@licencas.local", Email = "admin@licencas.local" }, "Senha@Forte123");

        var resultado = await service.LoginAsync(new LoginDto { Email = "admin@licencas.local", Senha = "SenhaErrada" });

        Assert.Null(resultado);
    }

    [Fact]
    public async Task LoginAsync_DeveRetornarNuloParaEmailInexistente()
    {
        var (service, _) = CriarService();

        var resultado = await service.LoginAsync(new LoginDto { Email = "naoexiste@licencas.local", Senha = "Senha@Forte123" });

        Assert.Null(resultado);
    }

    [Fact]
    public async Task DefinirSenhaAsync_DevePermitirLoginComANovaSenha()
    {
        var (service, userManager) = CriarService();
        var usuario = new ApplicationUser { UserName = "colaborador@empresa.com", Email = "colaborador@empresa.com" };
        await userManager.CreateAsync(usuario, "SenhaTemporaria@Aa1!");
        var token = await userManager.GeneratePasswordResetTokenAsync(usuario);

        await service.DefinirSenhaAsync(new DefinirSenhaDto { Email = "colaborador@empresa.com", Token = token, NovaSenha = "NovaSenha@Forte123" });

        var login = await service.LoginAsync(new LoginDto { Email = "colaborador@empresa.com", Senha = "NovaSenha@Forte123" });
        Assert.NotNull(login);
    }

    [Fact]
    public async Task DefinirSenhaAsync_DeveRejeitarEmailInexistente()
    {
        var (service, _) = CriarService();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.DefinirSenhaAsync(new DefinirSenhaDto { Email = "naoexiste@empresa.com", Token = "qualquer", NovaSenha = "NovaSenha@Forte123" }));
    }

    [Fact]
    public async Task DefinirSenhaAsync_DeveRejeitarTokenInvalido()
    {
        var (service, userManager) = CriarService();
        var usuario = new ApplicationUser { UserName = "colaborador@empresa.com", Email = "colaborador@empresa.com" };
        await userManager.CreateAsync(usuario, "SenhaTemporaria@Aa1!");

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.DefinirSenhaAsync(new DefinirSenhaDto { Email = "colaborador@empresa.com", Token = "token-invalido", NovaSenha = "NovaSenha@Forte123" }));
    }

    [Fact]
    public async Task DefinirSenhaAsync_DeveRejeitarSenhaQueNaoAtendeAPolitica()
    {
        var (service, userManager) = CriarService();
        var usuario = new ApplicationUser { UserName = "colaborador@empresa.com", Email = "colaborador@empresa.com" };
        await userManager.CreateAsync(usuario, "SenhaTemporaria@Aa1!");
        var token = await userManager.GeneratePasswordResetTokenAsync(usuario);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.DefinirSenhaAsync(new DefinirSenhaDto { Email = "colaborador@empresa.com", Token = token, NovaSenha = "123" }));
    }
}

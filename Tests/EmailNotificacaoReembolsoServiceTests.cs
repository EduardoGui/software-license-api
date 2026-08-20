using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class EmailNotificacaoReembolsoServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private static EmailNotificacaoReembolsoService CriarService(out AppDbContext context)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        context = new AppDbContext(options);
        return new EmailNotificacaoReembolsoService(context, new FakeTimeProvider(Agora), NullLogger<EmailNotificacaoReembolsoService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_DeveCriarComAtivoPadraoVerdadeiro()
    {
        var service = CriarService(out _);

        var email = await service.CreateAsync(new CreateEmailNotificacaoReembolsoDto { Email = "financeiro@hope-br.com", TipoDestinatario = TipoDestinatarioEmail.Para });

        Assert.True(email.Ativo);
        Assert.Equal(TipoDestinatarioEmail.Para, email.TipoDestinatario);
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarTipoDestinatarioInvalido()
    {
        var service = CriarService(out _);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new CreateEmailNotificacaoReembolsoDto { Email = "financeiro@hope-br.com", TipoDestinatario = "Bcc" }));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarEmailDuplicado()
    {
        var service = CriarService(out _);
        await service.CreateAsync(new CreateEmailNotificacaoReembolsoDto { Email = "financeiro@hope-br.com", TipoDestinatario = TipoDestinatarioEmail.Para });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new CreateEmailNotificacaoReembolsoDto { Email = "financeiro@hope-br.com", TipoDestinatario = TipoDestinatarioEmail.Cc }));
    }

    [Fact]
    public async Task UpdateAsync_DevePermitirTrocarTipoDestinatario()
    {
        var service = CriarService(out _);
        var criado = await service.CreateAsync(new CreateEmailNotificacaoReembolsoDto { Email = "financeiro@hope-br.com", TipoDestinatario = TipoDestinatarioEmail.Para });

        var atualizado = await service.UpdateAsync(criado.Id, new UpdateEmailNotificacaoReembolsoDto
        {
            Email = "financeiro@hope-br.com", TipoDestinatario = TipoDestinatarioEmail.Cc, Ativo = false,
        });

        Assert.Equal(TipoDestinatarioEmail.Cc, atualizado.TipoDestinatario);
        Assert.False(atualizado.Ativo);
    }

    [Fact]
    public async Task GetByIdAsync_DeveLancarNotFoundParaEmailInexistente()
    {
        var service = CriarService(out _);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(999));
    }

    [Fact]
    public async Task GetAllAsync_DeveFiltrarPorAtivo()
    {
        var service = CriarService(out _);
        var ativo = await service.CreateAsync(new CreateEmailNotificacaoReembolsoDto { Email = "financeiro@hope-br.com", TipoDestinatario = TipoDestinatarioEmail.Para });
        var inativo = await service.CreateAsync(new CreateEmailNotificacaoReembolsoDto { Email = "antigo@hope-br.com", TipoDestinatario = TipoDestinatarioEmail.Para, Ativo = false });

        var resultado = await service.GetAllAsync(new EmailNotificacaoReembolsoFiltroDto { Ativo = true });

        Assert.Single(resultado);
        Assert.Equal(ativo.Id, resultado[0].Id);
        Assert.NotEqual(inativo.Id, resultado[0].Id);
    }
}

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

public class UsuarioServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);

    private static UsuarioService CriarService(out AppDbContext context)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        context = new AppDbContext(options);

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
        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var papel in new[] { Roles.Administrador, Roles.Colaborador })
        {
            roleManager.CreateAsync(new IdentityRole(papel)).GetAwaiter().GetResult();
        }

        var configuracao = new ConfigurationBuilder().AddInMemoryCollection().Build();

        return new UsuarioService(
            context,
            new FakeTimeProvider(Agora),
            NullLogger<UsuarioService>.Instance,
            userManager,
            new LogEmailSender(NullLogger<LogEmailSender>.Instance),
            configuracao);
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarEmailDuplicado()
    {
        var service = CriarService(out _);
        var dto = new CreateUsuarioDto { Nome = "Ana", Email = "ana@empresa.com", DataInicio = DateOnly.FromDateTime(Agora.Date) };

        await service.CreateAsync(dto);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarDataFimAnteriorADataInicio()
    {
        var service = CriarService(out _);
        var inicio = DateOnly.FromDateTime(Agora.Date);
        var dto = new CreateUsuarioDto { Nome = "Ana", Email = "ana@empresa.com", DataInicio = inicio, DataFim = inicio.AddDays(-1) };

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_DeveCalcularStatusAgendadoQuandoInicioNoFuturo()
    {
        var service = CriarService(out _);
        var futuro = DateOnly.FromDateTime(Agora.Date).AddDays(10);
        var dto = new CreateUsuarioDto { Nome = "Ana", Email = "ana@empresa.com", DataInicio = futuro };

        var usuario = await service.CreateAsync(dto);

        Assert.Equal(UsuarioStatus.Agendado, usuario.Status);
    }

    [Fact]
    public async Task DesativarAsync_DevePreencherDataFimETornarInativo()
    {
        var service = CriarService(out _);
        var inicio = DateOnly.FromDateTime(Agora.Date).AddDays(-30);
        var criado = await service.CreateAsync(new CreateUsuarioDto { Nome = "Ana", Email = "ana@empresa.com", DataInicio = inicio });

        var desativado = await service.DesativarAsync(criado.Id, new DesativarUsuarioDto());

        Assert.Equal(DateOnly.FromDateTime(Agora.Date), desativado.DataFim);
        Assert.Equal(UsuarioStatus.Inativo, desativado.Status);
    }

    [Fact]
    public async Task GetByIdAsync_DeveLancarNotFoundParaUsuarioInexistente()
    {
        var service = CriarService(out _);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(999));
    }

    [Fact]
    public async Task DesativarAsync_DeveEncerrarMovimentacoesAtivasELiberarLicencas()
    {
        var service = CriarService(out var context);
        var inicio = DateOnly.FromDateTime(Agora.Date).AddDays(-30);
        var usuario = await service.CreateAsync(new CreateUsuarioDto { Nome = "Ana", Email = "ana@empresa.com", DataInicio = inicio });

        var licenca = new Licenca
        {
            Nome = "Microsoft 365",
            QuantidadeTotal = 1,
            DataInicio = inicio,
            DataTerminoPrevisto = DateOnly.FromDateTime(Agora.Date).AddYears(1),
            DiasAntecedenciaAviso = 30,
            Ativa = true,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Licencas.Add(licenca);
        await context.SaveChangesAsync();

        var movimentacao = new UsuarioLicenca
        {
            UsuarioId = usuario.Id,
            LicencaId = licenca.Id,
            DataInicio = inicio,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.UsuarioLicencas.Add(movimentacao);
        await context.SaveChangesAsync();

        await service.DesativarAsync(usuario.Id, new DesativarUsuarioDto());

        var movimentacaoAtualizada = await context.UsuarioLicencas.FindAsync(movimentacao.Id);
        Assert.NotNull(movimentacaoAtualizada!.DataFim);

        var emUso = await context.UsuarioLicencas.CountAsync(m => m.LicencaId == licenca.Id && m.DataFim == null);
        Assert.Equal(0, emUso);
    }

    [Fact]
    public async Task DesativarAsync_DeveEncerrarAlocacoesDeEquipamentoAtivas()
    {
        var service = CriarService(out var context);
        var inicio = DateOnly.FromDateTime(Agora.Date).AddDays(-30);
        var usuario = await service.CreateAsync(new CreateUsuarioDto { Nome = "Ana", Email = "ana@empresa.com", DataInicio = inicio });

        var tipo = new TipoEquipamento { Nome = "Notebook", Ativo = true, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        context.TiposEquipamento.Add(tipo);
        await context.SaveChangesAsync();

        var equipamento = new Equipamento
        {
            TipoEquipamentoId = tipo.Id,
            Origem = "Comprado",
            Status = "Disponivel",
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Equipamentos.Add(equipamento);
        await context.SaveChangesAsync();

        var alocacao = new EquipamentoAlocacao
        {
            EquipamentoId = equipamento.Id,
            UsuarioId = usuario.Id,
            DataInicio = inicio,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.EquipamentoAlocacoes.Add(alocacao);
        await context.SaveChangesAsync();

        await service.DesativarAsync(usuario.Id, new DesativarUsuarioDto());

        var alocacaoAtualizada = await context.EquipamentoAlocacoes.FindAsync(alocacao.Id);
        Assert.NotNull(alocacaoAtualizada!.DataFim);
    }

    [Fact]
    public async Task DesativarAsync_NaoDeveAlterarMovimentacoesJaEncerradasDeOutrosUsuarios()
    {
        var service = CriarService(out var context);
        var inicio = DateOnly.FromDateTime(Agora.Date).AddDays(-30);
        var usuarioAlvo = await service.CreateAsync(new CreateUsuarioDto { Nome = "Ana", Email = "ana@empresa.com", DataInicio = inicio });
        var outroUsuario = await service.CreateAsync(new CreateUsuarioDto { Nome = "João", Email = "joao@empresa.com", DataInicio = inicio });

        var licenca = new Licenca
        {
            Nome = "Microsoft 365",
            QuantidadeTotal = 2,
            DataInicio = inicio,
            DataTerminoPrevisto = DateOnly.FromDateTime(Agora.Date).AddYears(1),
            DiasAntecedenciaAviso = 30,
            Ativa = true,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Licencas.Add(licenca);
        await context.SaveChangesAsync();

        var movimentacaoDoOutro = new UsuarioLicenca
        {
            UsuarioId = outroUsuario.Id,
            LicencaId = licenca.Id,
            DataInicio = inicio,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.UsuarioLicencas.Add(movimentacaoDoOutro);
        await context.SaveChangesAsync();

        await service.DesativarAsync(usuarioAlvo.Id, new DesativarUsuarioDto());

        var movimentacaoInalterada = await context.UsuarioLicencas.FindAsync(movimentacaoDoOutro.Id);
        Assert.Null(movimentacaoInalterada!.DataFim);
    }

    [Fact]
    public async Task GetByIdAsync_DeveContarLicencasEmUsoCorretamente()
    {
        var service = CriarService(out var context);
        var inicio = DateOnly.FromDateTime(Agora.Date).AddDays(-30);
        var usuario = await service.CreateAsync(new CreateUsuarioDto { Nome = "Ana", Email = "ana@empresa.com", DataInicio = inicio });

        var licenca = new Licenca
        {
            Nome = "Microsoft 365",
            QuantidadeTotal = 5,
            DataInicio = inicio,
            DataTerminoPrevisto = DateOnly.FromDateTime(Agora.Date).AddYears(1),
            DiasAntecedenciaAviso = 30,
            Ativa = true,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Licencas.Add(licenca);
        await context.SaveChangesAsync();

        context.UsuarioLicencas.Add(new UsuarioLicenca
        {
            UsuarioId = usuario.Id,
            LicencaId = licenca.Id,
            DataInicio = inicio,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        });
        await context.SaveChangesAsync();

        var encontrado = await service.GetByIdAsync(usuario.Id);

        Assert.Equal(1, encontrado.LicencasEmUso);
    }

    [Fact]
    public async Task AtualizarPerfilAsync_DeveAtualizarCamposEIncluirNomeDoSetor()
    {
        var service = CriarService(out var context);
        var inicio = DateOnly.FromDateTime(Agora.Date);
        var usuario = await service.CreateAsync(new CreateUsuarioDto { Nome = "Ana", Email = "ana@empresa.com", DataInicio = inicio });

        var setor = new Setor { Nome = "Financeiro", Ativo = true, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        context.Setores.Add(setor);
        await context.SaveChangesAsync();

        var atualizado = await service.AtualizarPerfilAsync(usuario.Id, new AtualizarPerfilDto
        {
            Cpf = "123.456.789-00",
            Cargo = "Analista",
            SetorId = setor.Id,
            ChavePix = "ana@empresa.com",
            Banco = "Banco X",
            Agencia = "0001",
            ContaBancaria = "12345-6",
        });

        Assert.Equal("123.456.789-00", atualizado.Cpf);
        Assert.Equal("Analista", atualizado.Cargo);
        Assert.Equal(setor.Id, atualizado.SetorId);
        Assert.Equal("Financeiro", atualizado.SetorNome);
        Assert.Equal("ana@empresa.com", atualizado.ChavePix);
    }

    [Fact]
    public async Task AtualizarPerfilAsync_DeveRejeitarSetorInexistente()
    {
        var service = CriarService(out _);
        var inicio = DateOnly.FromDateTime(Agora.Date);
        var usuario = await service.CreateAsync(new CreateUsuarioDto { Nome = "Ana", Email = "ana@empresa.com", DataInicio = inicio });

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.AtualizarPerfilAsync(usuario.Id, new AtualizarPerfilDto { SetorId = 999 }));
    }

    [Fact]
    public async Task ReenviarConviteAsync_DeveLancarNotFoundParaUsuarioInexistente()
    {
        var service = CriarService(out _);

        await Assert.ThrowsAsync<NotFoundException>(() => service.ReenviarConviteAsync(999));
    }

    [Fact]
    public async Task ReenviarConviteAsync_DeveExecutarSemErroParaUsuarioComContaDeAcesso()
    {
        var service = CriarService(out _);
        var inicio = DateOnly.FromDateTime(Agora.Date);
        var usuario = await service.CreateAsync(new CreateUsuarioDto { Nome = "Ana", Email = "ana@empresa.com", DataInicio = inicio });

        await service.ReenviarConviteAsync(usuario.Id);
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarTipoPjSemEmpresaPj()
    {
        var service = CriarService(out _);
        var inicio = DateOnly.FromDateTime(Agora.Date);
        var dto = new CreateUsuarioDto { Nome = "Ana", Email = "ana@empresa.com", DataInicio = inicio, Tipo = UsuarioTipo.Pj };

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarEmpresaPjParaTipoDiferenteDePj()
    {
        var service = CriarService(out var context);
        var empresa = await CriarEmpresaPjAsync(context);
        var inicio = DateOnly.FromDateTime(Agora.Date);
        var dto = new CreateUsuarioDto
        {
            Nome = "Ana", Email = "ana@empresa.com", DataInicio = inicio, Tipo = UsuarioTipo.Clt, EmpresaPjId = empresa.Id,
        };

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarEmpresaPjInexistente()
    {
        var service = CriarService(out _);
        var inicio = DateOnly.FromDateTime(Agora.Date);
        var dto = new CreateUsuarioDto
        {
            Nome = "Ana", Email = "ana@empresa.com", DataInicio = inicio, Tipo = UsuarioTipo.Pj, EmpresaPjId = 999,
        };

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_DevePermitirTipoPjComEmpresaPjValida()
    {
        var service = CriarService(out var context);
        var empresa = await CriarEmpresaPjAsync(context);
        var inicio = DateOnly.FromDateTime(Agora.Date);

        var usuario = await service.CreateAsync(new CreateUsuarioDto
        {
            Nome = "Ana", Email = "ana@empresa.com", DataInicio = inicio, Tipo = UsuarioTipo.Pj, EmpresaPjId = empresa.Id,
        });

        Assert.Equal(UsuarioTipo.Pj, usuario.Tipo);
        Assert.Equal(empresa.Id, usuario.EmpresaPjId);
        Assert.Equal(empresa.RazaoSocial, usuario.EmpresaPjNome);
    }

    [Fact]
    public async Task AdicionarDependenteAsync_DeveAdicionarERetornarNaListaDoUsuario()
    {
        var service = CriarService(out _);
        var inicio = DateOnly.FromDateTime(Agora.Date);
        var usuario = await service.CreateAsync(new CreateUsuarioDto { Nome = "Ana", Email = "ana@empresa.com", DataInicio = inicio });

        var atualizado = await service.AdicionarDependenteAsync(usuario.Id, new CreateDependenteDto { Nome = "Maria" });

        var dependente = Assert.Single(atualizado.Dependentes);
        Assert.Equal("Maria", dependente.Nome);
        Assert.True(dependente.Ativo);
    }

    [Fact]
    public async Task AtualizarDependenteAsync_DeveAtualizarNomeEAtivo()
    {
        var service = CriarService(out _);
        var inicio = DateOnly.FromDateTime(Agora.Date);
        var usuario = await service.CreateAsync(new CreateUsuarioDto { Nome = "Ana", Email = "ana@empresa.com", DataInicio = inicio });
        var comDependente = await service.AdicionarDependenteAsync(usuario.Id, new CreateDependenteDto { Nome = "Maria" });
        var dependenteId = comDependente.Dependentes[0].Id;

        var atualizado = await service.AtualizarDependenteAsync(
            usuario.Id, dependenteId, new UpdateDependenteDto { Nome = "Maria Silva", Ativo = false });

        var dependente = Assert.Single(atualizado.Dependentes);
        Assert.Equal("Maria Silva", dependente.Nome);
        Assert.False(dependente.Ativo);
    }

    [Fact]
    public async Task RemoverDependenteAsync_DeveRemoverDaListaDoUsuario()
    {
        var service = CriarService(out _);
        var inicio = DateOnly.FromDateTime(Agora.Date);
        var usuario = await service.CreateAsync(new CreateUsuarioDto { Nome = "Ana", Email = "ana@empresa.com", DataInicio = inicio });
        var comDependente = await service.AdicionarDependenteAsync(usuario.Id, new CreateDependenteDto { Nome = "Maria" });
        var dependenteId = comDependente.Dependentes[0].Id;

        var atualizado = await service.RemoverDependenteAsync(usuario.Id, dependenteId);

        Assert.Empty(atualizado.Dependentes);
    }

    [Fact]
    public async Task RemoverDependenteAsync_DeveLancarNotFoundQuandoDependenteNaoPertenceAoUsuario()
    {
        var service = CriarService(out _);
        var inicio = DateOnly.FromDateTime(Agora.Date);
        var usuarioA = await service.CreateAsync(new CreateUsuarioDto { Nome = "Ana", Email = "ana@empresa.com", DataInicio = inicio });
        var usuarioB = await service.CreateAsync(new CreateUsuarioDto { Nome = "Bruno", Email = "bruno@empresa.com", DataInicio = inicio });
        var comDependente = await service.AdicionarDependenteAsync(usuarioA.Id, new CreateDependenteDto { Nome = "Maria" });
        var dependenteId = comDependente.Dependentes[0].Id;

        await Assert.ThrowsAsync<NotFoundException>(() => service.RemoverDependenteAsync(usuarioB.Id, dependenteId));
    }

    private static async Task<EmpresaPj> CriarEmpresaPjAsync(AppDbContext context)
    {
        var empresa = new EmpresaPj
        {
            RazaoSocial = "Consultoria XYZ Ltda",
            Cnpj = "12.345.678/0001-90",
            Ativa = true,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.EmpresasPj.Add(empresa);
        await context.SaveChangesAsync();
        return empresa;
    }
}

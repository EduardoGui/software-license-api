using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class FaturaOperadoraSaudeServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

    private static (FaturaOperadoraSaudeService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var service = new FaturaOperadoraSaudeService(context, new FakeTimeProvider(Agora), NullLogger<FaturaOperadoraSaudeService>.Instance);
        return (service, context);
    }

    private static Usuario CriarUsuario(AppDbContext context, string nome)
    {
        var usuario = new Usuario
        {
            Nome = nome,
            Email = $"{nome.ToLowerInvariant().Replace(" ", ".")}@empresa.com",
            DataInicio = new DateOnly(2020, 1, 1),
            Tipo = UsuarioTipo.Pj,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Usuarios.Add(usuario);
        context.SaveChanges();
        return usuario;
    }

    private static NotaDebitoPj CriarNotaVinculada(AppDbContext context, int usuarioId, int faturaId, int ano, int mes)
    {
        var nota = new NotaDebitoPj
        {
            UsuarioId = usuarioId,
            FaturaOperadoraSaudeId = faturaId,
            Ano = ano,
            Mes = mes,
            ValorBruto = 150m,
            OperadoraSaude = "AMIL",
            NumeroFatura = "71264082",
            Status = NotaDebitoPjStatus.Rascunho,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.NotasDebitoPj.Add(nota);
        context.SaveChanges();
        return nota;
    }

    private static CreateFaturaOperadoraSaudeDto CriarDto(string operadora = "AMIL", int ano = 2026, int mes = 8) => new()
    {
        OperadoraSaude = operadora,
        NumeroFatura = "71264082",
        Ano = ano,
        Mes = mes,
        DataEmissao = new DateOnly(ano, mes, 20),
        DataVencimento = new DateOnly(ano, mes, 28),
        ValorTotal = 1000m,
    };

    [Fact]
    public async Task CreateAsync_DeveCriarFatura()
    {
        var (service, _) = CriarService();

        var fatura = await service.CreateAsync(CriarDto());

        Assert.Equal("AMIL", fatura.OperadoraSaude);
        Assert.Equal("71264082", fatura.NumeroFatura);
        Assert.Equal(0, fatura.QuantidadeNotasDebito);
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarMesInvalido()
    {
        var (service, _) = CriarService();
        var dto = CriarDto();
        dto.Mes = 13;

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarDuplicidadeDeOperadoraAnoMes()
    {
        var (service, _) = CriarService();
        await service.CreateAsync(CriarDto());

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(CriarDto()));
    }

    [Fact]
    public async Task UpdateAsync_DevePermitirEditarNumeroDatasEValor()
    {
        var (service, _) = CriarService();
        var criada = await service.CreateAsync(CriarDto());

        var atualizada = await service.UpdateAsync(criada.Id, new UpdateFaturaOperadoraSaudeDto
        {
            NumeroFatura = "99999999",
            DataEmissao = new DateOnly(2026, 8, 21),
            DataVencimento = new DateOnly(2026, 8, 30),
            ValorTotal = 1500m,
            Observacao = "Corrigido",
        });

        Assert.Equal("99999999", atualizada.NumeroFatura);
        Assert.Equal(new DateOnly(2026, 8, 21), atualizada.DataEmissao);
        Assert.Equal(1500m, atualizada.ValorTotal);
    }

    [Fact]
    public async Task UpdateAsync_DeveSincronizarNotasDeDebitoVinculadas()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuario(context, "João Pj");
        var criada = await service.CreateAsync(CriarDto());
        CriarNotaVinculada(context, usuario.Id, criada.Id, 2026, 8);

        await service.UpdateAsync(criada.Id, new UpdateFaturaOperadoraSaudeDto
        {
            NumeroFatura = "88888888",
            DataVencimento = new DateOnly(2026, 8, 31),
        });

        var nota = await context.NotasDebitoPj.FirstAsync(n => n.UsuarioId == usuario.Id);
        Assert.Equal("88888888", nota.NumeroFatura);
        Assert.Equal(new DateOnly(2026, 8, 31), nota.DataVencimento);
    }

    [Fact]
    public async Task DeleteAsync_DeveRejeitarQuandoTemNotaVinculada()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuario(context, "João Pj");
        var criada = await service.CreateAsync(CriarDto());
        CriarNotaVinculada(context, usuario.Id, criada.Id, 2026, 8);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.DeleteAsync(criada.Id));
    }

    [Fact]
    public async Task DeleteAsync_DevePermitirExcluirSemNotaVinculada()
    {
        var (service, context) = CriarService();
        var criada = await service.CreateAsync(CriarDto());

        await service.DeleteAsync(criada.Id);

        Assert.Empty(context.FaturasOperadoraSaude);
    }

    [Fact]
    public async Task GetAllAsync_DeveFiltrarPorAnoMesEOperadora()
    {
        var (service, _) = CriarService();
        await service.CreateAsync(CriarDto("AMIL", 2026, 8));
        await service.CreateAsync(CriarDto("Bradesco Saúde", 2026, 8));
        await service.CreateAsync(CriarDto("AMIL", 2026, 9));

        var resultado = await service.GetAllAsync(new FaturaOperadoraSaudeFiltroDto { Ano = 2026, Mes = 8, OperadoraSaude = "AMIL" });

        var item = Assert.Single(resultado);
        Assert.Equal("AMIL", item.OperadoraSaude);
    }

    [Fact]
    public async Task AnexoFluxoCompleto_DeveAdicionarListarEExcluir()
    {
        var (service, _) = CriarService();
        var criada = await service.CreateAsync(CriarDto());

        var anexo = await service.AdicionarAnexoAsync(criada.Id, new AdicionarAnexoDto
        {
            NomeArquivo = "fatura.pdf",
            TipoConteudo = "application/pdf",
            Conteudo = [1, 2, 3],
        });

        var lista = await service.ListarAnexosAsync(criada.Id);
        Assert.Single(lista);

        var arquivo = await service.ObterAnexoAsync(criada.Id, anexo.Id);
        Assert.Equal("fatura.pdf", arquivo.NomeArquivo);

        await service.ExcluirAnexoAsync(criada.Id, anexo.Id);
        Assert.Empty(await service.ListarAnexosAsync(criada.Id));
    }
}

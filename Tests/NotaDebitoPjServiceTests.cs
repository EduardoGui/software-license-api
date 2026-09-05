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

public class NotaDebitoPjServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    private static (NotaDebitoPjService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var configuracao = new ConfigurationBuilder().AddInMemoryCollection().Build();
        return (new NotaDebitoPjService(context, new FakeTimeProvider(Agora), NullLogger<NotaDebitoPjService>.Instance, configuracao), context);
    }

    private static (NotaDebitoPjService Service, AppDbContext Context) CriarServiceComConfiguracaoPix()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReembolsoDespesa:EmpresaNome"] = "SPE Hope S.A.",
                ["ReembolsoDespesa:EmpresaCnpj"] = "63.523.589/0001-22",
                ["ReembolsoDespesa:EmpresaCidade"] = "Belo Horizonte",
            })
            .Build();
        return (new NotaDebitoPjService(context, new FakeTimeProvider(Agora), NullLogger<NotaDebitoPjService>.Instance, configuracao), context);
    }

    private static EmpresaPj CriarEmpresaPj(AppDbContext context, string razaoSocial, string cnpj)
    {
        var empresa = new EmpresaPj
        {
            RazaoSocial = razaoSocial,
            Cnpj = cnpj,
            Ativa = true,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.EmpresasPj.Add(empresa);
        context.SaveChanges();
        return empresa;
    }

    private static Usuario CriarUsuario(AppDbContext context, string nome, string? tipo = null, int? empresaPjId = null)
    {
        var usuario = new Usuario
        {
            Nome = nome,
            Email = $"{nome.ToLowerInvariant().Replace(" ", ".")}@empresa.com",
            DataInicio = new DateOnly(2020, 1, 1),
            Tipo = tipo,
            Cpf = "123.456.789-00",
            EmpresaPjId = empresaPjId,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Usuarios.Add(usuario);
        context.SaveChanges();
        return usuario;
    }

    private static void CriarLancamento(AppDbContext context, int usuarioId, int ano, int mes, decimal valorCoparticipacao)
    {
        context.PlanoSaudeCustos.Add(new PlanoSaudeCusto
        {
            UsuarioId = usuarioId,
            DependenteId = null,
            Ano = ano,
            Mes = mes,
            ValorMensal = 0,
            ValorCoparticipacao = valorCoparticipacao,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        });
        context.SaveChanges();
    }

    private static CreateNotaDebitoPjDto CriarDto(int usuarioId, int ano = 2026, int mes = 8) => new()
    {
        UsuarioId = usuarioId,
        Ano = ano,
        Mes = mes,
        OperadoraSaude = "AMIL",
    };

    [Fact]
    public async Task CreateAsync_DeveCalcularValorBrutoComoSomaDaCoparticipacao()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuario(context, "João Pj", UsuarioTipo.Pj);
        CriarLancamento(context, usuario.Id, 2026, 8, 300m);

        var nota = await service.CreateAsync(CriarDto(usuario.Id));

        Assert.Equal(300m, nota.ValorBruto);
        Assert.Equal(300m, nota.ValorLiquido);
        Assert.Equal(NotaDebitoPjStatus.Rascunho, nota.Status);
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarUsuarioQueNaoEhPj()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuario(context, "Maria Clt", UsuarioTipo.Clt);
        CriarLancamento(context, usuario.Id, 2026, 8, 300m);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(CriarDto(usuario.Id)));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarSemCoparticipacaoLancada()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuario(context, "João Pj", UsuarioTipo.Pj);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(CriarDto(usuario.Id)));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarSegundaNotaParaMesmoUsuarioEMes()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuario(context, "João Pj", UsuarioTipo.Pj);
        CriarLancamento(context, usuario.Id, 2026, 8, 300m);
        await service.CreateAsync(CriarDto(usuario.Id));

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(CriarDto(usuario.Id)));
    }

    [Fact]
    public async Task UpdateAsync_DevePermitirEditarEnquantoRascunho()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuario(context, "João Pj", UsuarioTipo.Pj);
        CriarLancamento(context, usuario.Id, 2026, 8, 300m);
        var criada = await service.CreateAsync(CriarDto(usuario.Id));

        var atualizada = await service.UpdateAsync(criada.Id, new UpdateNotaDebitoPjDto
        {
            OperadoraSaude = "Bradesco Saúde",
            Desconto = 10m,
            RetencaoTributaria = 5m,
        });

        Assert.Equal("Bradesco Saúde", atualizada.OperadoraSaude);
        Assert.Equal(285m, atualizada.ValorLiquido);
    }

    [Fact]
    public async Task UpdateAsync_DeveRejeitarEdicaoAposEnviada()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuario(context, "João Pj", UsuarioTipo.Pj);
        CriarLancamento(context, usuario.Id, 2026, 8, 300m);
        var criada = await service.CreateAsync(CriarDto(usuario.Id));
        await service.EnviarAsync(criada.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UpdateAsync(criada.Id, new UpdateNotaDebitoPjDto { OperadoraSaude = "Outra" }));
    }

    [Fact]
    public async Task DeleteAsync_DeveRejeitarExclusaoAposEnviada()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuario(context, "João Pj", UsuarioTipo.Pj);
        CriarLancamento(context, usuario.Id, 2026, 8, 300m);
        var criada = await service.CreateAsync(CriarDto(usuario.Id));
        await service.EnviarAsync(criada.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.DeleteAsync(criada.Id));
    }

    [Fact]
    public async Task DeleteAsync_DevePermitirExcluirRascunho()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuario(context, "João Pj", UsuarioTipo.Pj);
        CriarLancamento(context, usuario.Id, 2026, 8, 300m);
        var criada = await service.CreateAsync(CriarDto(usuario.Id));

        await service.DeleteAsync(criada.Id);

        Assert.Empty(context.NotasDebitoPj);
    }

    [Fact]
    public async Task EnviarAsync_DeveMudarStatusEGravarDataEnvio()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuario(context, "João Pj", UsuarioTipo.Pj);
        CriarLancamento(context, usuario.Id, 2026, 8, 300m);
        var criada = await service.CreateAsync(CriarDto(usuario.Id));

        var enviada = await service.EnviarAsync(criada.Id);

        Assert.Equal(NotaDebitoPjStatus.Enviada, enviada.Status);
        Assert.NotNull(enviada.DataEnvio);
    }

    [Fact]
    public async Task EnviarAsync_DeveRejeitarSeJaEnviada()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuario(context, "João Pj", UsuarioTipo.Pj);
        CriarLancamento(context, usuario.Id, 2026, 8, 300m);
        var criada = await service.CreateAsync(CriarDto(usuario.Id));
        await service.EnviarAsync(criada.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.EnviarAsync(criada.Id));
    }

    [Fact]
    public async Task PagarAsync_DeveRejeitarSeAindaNaoEnviada()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuario(context, "João Pj", UsuarioTipo.Pj);
        CriarLancamento(context, usuario.Id, 2026, 8, 300m);
        var criada = await service.CreateAsync(CriarDto(usuario.Id));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.PagarAsync(criada.Id, new PagarNotaDebitoPjDto { DataPagamento = new DateOnly(2026, 9, 1) }));
    }

    [Fact]
    public async Task PagarAsync_DeveMudarStatusEGravarDataPagamento()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuario(context, "João Pj", UsuarioTipo.Pj);
        CriarLancamento(context, usuario.Id, 2026, 8, 300m);
        var criada = await service.CreateAsync(CriarDto(usuario.Id));
        await service.EnviarAsync(criada.Id);

        var paga = await service.PagarAsync(criada.Id, new PagarNotaDebitoPjDto { DataPagamento = new DateOnly(2026, 9, 5) });

        Assert.Equal(NotaDebitoPjStatus.Paga, paga.Status);
        Assert.Equal(new DateOnly(2026, 9, 5), paga.DataPagamento);
    }

    [Fact]
    public async Task GetAllAsync_DeveFiltrarPorStatus()
    {
        var (service, context) = CriarService();
        var usuario1 = CriarUsuario(context, "João Pj", UsuarioTipo.Pj);
        var usuario2 = CriarUsuario(context, "Carlos Pj", UsuarioTipo.Pj);
        CriarLancamento(context, usuario1.Id, 2026, 8, 300m);
        CriarLancamento(context, usuario2.Id, 2026, 8, 200m);
        var nota1 = await service.CreateAsync(CriarDto(usuario1.Id));
        await service.CreateAsync(CriarDto(usuario2.Id));
        await service.EnviarAsync(nota1.Id);

        var resultado = await service.GetAllAsync(new NotaDebitoPjFiltroDto { Status = NotaDebitoPjStatus.Enviada });

        var item = Assert.Single(resultado);
        Assert.Equal(nota1.Id, item.Id);
    }

    [Fact]
    public async Task GerarPdfAsync_DeveGerarArquivoNaoVazio()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuario(context, "João Pj", UsuarioTipo.Pj);
        CriarLancamento(context, usuario.Id, 2026, 8, 300m);
        var criada = await service.CreateAsync(CriarDto(usuario.Id));

        var pdf = await service.GerarPdfAsync(criada.Id);

        Assert.NotEmpty(pdf);
    }

    [Fact]
    public async Task GerarPdfAsync_DeveGerarQrCodePixQuandoEmpresaConfigurada()
    {
        var (service, context) = CriarServiceComConfiguracaoPix();
        var usuario = CriarUsuario(context, "João Pj", UsuarioTipo.Pj);
        CriarLancamento(context, usuario.Id, 2026, 8, 300m);
        var criada = await service.CreateAsync(CriarDto(usuario.Id));

        var pdfSemQrCode = await new NotaDebitoPjService(
            context, new FakeTimeProvider(Agora), NullLogger<NotaDebitoPjService>.Instance,
            new ConfigurationBuilder().AddInMemoryCollection().Build()).GerarPdfAsync(criada.Id);
        var pdfComQrCode = await service.GerarPdfAsync(criada.Id);

        Assert.NotEmpty(pdfComQrCode);
        // A imagem do QR code embutida deve deixar o arquivo sensivelmente maior que a versão sem Pix configurado.
        Assert.True(pdfComQrCode.Length > pdfSemQrCode.Length);
    }

    [Fact]
    public async Task CreateAsync_DeveIncluirRazaoSocialECnpjDaEmpresaPjNoDto()
    {
        var (service, context) = CriarService();
        var empresa = CriarEmpresaPj(context, "Empresa Teste LTDA", "11.222.333/0001-44");
        var usuario = CriarUsuario(context, "João Pj", UsuarioTipo.Pj, empresa.Id);
        CriarLancamento(context, usuario.Id, 2026, 8, 300m);

        var nota = await service.CreateAsync(CriarDto(usuario.Id));

        Assert.Equal("Empresa Teste LTDA", nota.EmpresaPjNome);
        Assert.Equal("11.222.333/0001-44", nota.EmpresaPjCnpj);
    }

    [Fact]
    public async Task AnexoFluxoCompleto_DeveAdicionarListarEExcluir()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuario(context, "João Pj", UsuarioTipo.Pj);
        CriarLancamento(context, usuario.Id, 2026, 8, 300m);
        var criada = await service.CreateAsync(CriarDto(usuario.Id));

        var anexo = await service.AdicionarAnexoAsync(criada.Id, new AdicionarAnexoDto
        {
            NomeArquivo = "recibo.pdf",
            TipoConteudo = "application/pdf",
            Conteudo = [1, 2, 3],
        });

        var lista = await service.ListarAnexosAsync(criada.Id);
        Assert.Single(lista);

        var arquivo = await service.ObterAnexoAsync(criada.Id, anexo.Id);
        Assert.Equal("recibo.pdf", arquivo.NomeArquivo);

        await service.ExcluirAnexoAsync(criada.Id, anexo.Id);
        Assert.Empty(await service.ListarAnexosAsync(criada.Id));
    }
}

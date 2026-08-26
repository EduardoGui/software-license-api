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

public class ReembolsoDespesaServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(Agora.Date);

    private static (ReembolsoDespesaService Service, AppDbContext Context, FakeEmailSender EmailSender) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var configuracao = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var emailSender = new FakeEmailSender();
        var timeProvider = new FakeTimeProvider(Agora);
        var auditoriaService = new AuditoriaService(context, timeProvider);
        var service = new ReembolsoDespesaService(
            context, timeProvider, NullLogger<ReembolsoDespesaService>.Instance, configuracao, emailSender, auditoriaService);
        return (service, context, emailSender);
    }

    private static Usuario CriarUsuario(AppDbContext context, string nome = "Ana")
    {
        var usuario = new Usuario
        {
            Nome = nome,
            Email = $"{nome.ToLowerInvariant()}@empresa.com",
            DataInicio = Hoje.AddYears(-1),
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Usuarios.Add(usuario);
        context.SaveChanges();
        return usuario;
    }

    private static void CompletarPerfil(AppDbContext context, Usuario usuario, int setorId)
    {
        usuario.Cpf = "123.456.789-00";
        usuario.Cargo = "Analista";
        usuario.SetorId = setorId;
        usuario.ChavePix = usuario.Email;
        usuario.Banco = "Banco X";
        usuario.Agencia = "0001";
        usuario.ContaBancaria = "12345-6";
        context.SaveChanges();
    }

    private static Setor CriarSetor(AppDbContext context, string nome = "Financeiro")
    {
        var setor = new Setor { Nome = nome, Ativo = true, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        context.Setores.Add(setor);
        context.SaveChanges();
        return setor;
    }

    private static void TornarAprovador(AppDbContext context, int setorId, int usuarioId)
    {
        context.SetorAprovadores.Add(new SetorAprovador { SetorId = setorId, UsuarioId = usuarioId, DataCriacao = Agora.UtcDateTime });
        context.SaveChanges();
    }

    private static TipoDespesa CriarTipoDespesa(AppDbContext context, string nome = "Combustível")
    {
        var tipo = new TipoDespesa { Nome = nome, Ativo = true, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        context.TiposDespesa.Add(tipo);
        context.SaveChanges();
        return tipo;
    }

    private static Local CriarLocal(AppDbContext context, string nome = "Obra Savassi")
    {
        var local = new Local { Nome = nome, Endereco = "Rua das Obras, 100", Ativo = true, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        context.Locais.Add(local);
        context.SaveChanges();
        return local;
    }

    private static CreateReembolsoDespesaDto CriarDto(int tipoDespesaId, decimal valor = 100m) => new()
    {
        Finalidade = "Viagem a cliente",
        Itens = [new CreateReembolsoDespesaItemDto { Data = Hoje, TipoDespesaId = tipoDespesaId, Descricao = "Gasolina", Valor = valor }],
    };

    [Fact]
    public async Task CreateAsync_DeveCriarComoRascunhoEValorTotalCorreto()
    {
        var (service, context, _) = CriarService();
        var usuario = CriarUsuario(context);
        var tipo = CriarTipoDespesa(context);

        var reembolso = await service.CreateAsync(usuario.Id, CriarDto(tipo.Id, 150.50m));

        Assert.Equal(ReembolsoDespesaStatus.Rascunho, reembolso.Status);
        Assert.Equal(150.50m, reembolso.ValorTotal);
        Assert.Equal(Hoje, reembolso.DataSolicitacao);
        Assert.Equal("PIX", reembolso.FormaPagamento);
        Assert.Single(reembolso.Itens);
        Assert.Equal("Combustível", reembolso.Itens[0].TipoDespesaNome);
    }

    [Fact]
    public async Task CreateAsync_DevePermitirRascunhoSemItens()
    {
        var (service, context, _) = CriarService();
        var usuario = CriarUsuario(context);

        var reembolso = await service.CreateAsync(usuario.Id, new CreateReembolsoDespesaDto { Finalidade = "Reunião" });

        Assert.Empty(reembolso.Itens);
        Assert.Equal(0m, reembolso.ValorTotal);
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarTipoDespesaInexistente()
    {
        var (service, context, _) = CriarService();
        var usuario = CriarUsuario(context);

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAsync(usuario.Id, CriarDto(999)));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarLocalInexistente()
    {
        var (service, context, _) = CriarService();
        var usuario = CriarUsuario(context);
        var tipo = CriarTipoDespesa(context);

        var dto = CriarDto(tipo.Id);
        dto.LocalId = 999;

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAsync(usuario.Id, dto));
    }

    [Fact]
    public async Task CreateAsync_DevePreencherLocalIdELocalNome()
    {
        var (service, context, _) = CriarService();
        var usuario = CriarUsuario(context);
        var tipo = CriarTipoDespesa(context);
        var local = CriarLocal(context);

        var dto = CriarDto(tipo.Id);
        dto.LocalId = local.Id;

        var reembolso = await service.CreateAsync(usuario.Id, dto);

        Assert.Equal(local.Id, reembolso.LocalId);
        Assert.Equal("Obra Savassi", reembolso.LocalNome);
    }

    [Fact]
    public async Task UpdateAsync_DeveSubstituirItensExistentes()
    {
        var (service, context, _) = CriarService();
        var usuario = CriarUsuario(context);
        var tipo = CriarTipoDespesa(context);
        var criado = await service.CreateAsync(usuario.Id, CriarDto(tipo.Id, 100m));

        var outroTipo = CriarTipoDespesa(context, "Hospedagem");
        var atualizado = await service.UpdateAsync(criado.Id, new UpdateReembolsoDespesaDto
        {
            Finalidade = "Viagem a cliente - atualizado",
            Itens =
            [
                new CreateReembolsoDespesaItemDto { Data = Hoje, TipoDespesaId = outroTipo.Id, Valor = 300m },
            ],
        });

        Assert.Equal("Viagem a cliente - atualizado", atualizado.Finalidade);
        var item = Assert.Single(atualizado.Itens);
        Assert.Equal("Hospedagem", item.TipoDespesaNome);
        Assert.Equal(300m, atualizado.ValorTotal);
    }

    [Fact]
    public async Task UpdateAsync_DeveRejeitarQuandoStatusNaoEditavel()
    {
        var (service, context, _) = CriarService();
        var usuario = CriarUsuario(context);
        var tipo = CriarTipoDespesa(context);
        var criado = await service.CreateAsync(usuario.Id, CriarDto(tipo.Id));

        var reembolso = await context.ReembolsosDespesa.FindAsync(criado.Id);
        reembolso!.Status = ReembolsoDespesaStatus.EnviadoParaAprovacao;
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UpdateAsync(criado.Id, new UpdateReembolsoDespesaDto { Finalidade = "Tentativa de edição" }));
    }

    [Fact]
    public async Task ExcluirAsync_DeveRemoverRascunhoEItens()
    {
        var (service, context, _) = CriarService();
        var usuario = CriarUsuario(context);
        var tipo = CriarTipoDespesa(context);
        var criado = await service.CreateAsync(usuario.Id, CriarDto(tipo.Id));

        await service.ExcluirAsync(criado.Id);

        Assert.Null(await context.ReembolsosDespesa.FindAsync(criado.Id));
        Assert.Empty(await context.ReembolsoDespesaItens.Where(i => i.ReembolsoDespesaId == criado.Id).ToListAsync());
    }

    [Fact]
    public async Task ExcluirAsync_DeveRejeitarQuandoStatusNaoEditavel()
    {
        var (service, context, _) = CriarService();
        var usuario = CriarUsuario(context);
        var tipo = CriarTipoDespesa(context);
        var criado = await service.CreateAsync(usuario.Id, CriarDto(tipo.Id));

        var reembolso = await context.ReembolsosDespesa.FindAsync(criado.Id);
        reembolso!.Status = ReembolsoDespesaStatus.Aprovado;
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ExcluirAsync(criado.Id));
    }

    [Fact]
    public async Task GetAllAsync_DeveFiltrarPorUsuarioEStatus()
    {
        var (service, context, _) = CriarService();
        var usuarioA = CriarUsuario(context, "Ana");
        var usuarioB = CriarUsuario(context, "Bruno");
        var tipo = CriarTipoDespesa(context);
        var deAna = await service.CreateAsync(usuarioA.Id, CriarDto(tipo.Id));
        await service.CreateAsync(usuarioB.Id, CriarDto(tipo.Id));

        var resultado = await service.GetAllAsync(new ReembolsoDespesaFiltroDto { UsuarioId = usuarioA.Id });

        var item = Assert.Single(resultado);
        Assert.Equal(deAna.Id, item.Id);
    }

    [Fact]
    public async Task GetByIdAsync_DeveLancarNotFoundParaReembolsoInexistente()
    {
        var (service, _, _) = CriarService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(999));
    }

    [Fact]
    public async Task EnviarAsync_DeveRejeitarPerfilIncompleto()
    {
        var (service, context, _) = CriarService();
        var usuario = CriarUsuario(context);
        var tipo = CriarTipoDespesa(context);
        var criado = await service.CreateAsync(usuario.Id, CriarDto(tipo.Id));

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.EnviarAsync(criado.Id));
    }

    [Fact]
    public async Task EnviarAsync_DeveRejeitarSemItens()
    {
        var (service, context, _) = CriarService();
        var usuario = CriarUsuario(context);
        var setor = CriarSetor(context);
        CompletarPerfil(context, usuario, setor.Id);
        var criado = await service.CreateAsync(usuario.Id, new CreateReembolsoDespesaDto { Finalidade = "Sem itens" });

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.EnviarAsync(criado.Id));
    }

    [Fact]
    public async Task EnviarAsync_DeveMudarStatusECopiarSetorDoUsuario()
    {
        var (service, context, _) = CriarService();
        var usuario = CriarUsuario(context);
        var setor = CriarSetor(context);
        CompletarPerfil(context, usuario, setor.Id);
        var tipo = CriarTipoDespesa(context);
        var criado = await service.CreateAsync(usuario.Id, CriarDto(tipo.Id));

        var enviado = await service.EnviarAsync(criado.Id);

        Assert.Equal(ReembolsoDespesaStatus.EnviadoParaAprovacao, enviado.Status);
        Assert.Equal(setor.Id, enviado.SetorId);
    }

    [Fact]
    public async Task AprovarAsync_DeveAprovarQuandoUsuarioEhAprovadorDoSetor()
    {
        var (service, context, _) = CriarService();
        var usuario = CriarUsuario(context, "Ana");
        var setor = CriarSetor(context);
        CompletarPerfil(context, usuario, setor.Id);
        var aprovador = CriarUsuario(context, "Bruno");
        TornarAprovador(context, setor.Id, aprovador.Id);
        var tipo = CriarTipoDespesa(context);
        var criado = await service.CreateAsync(usuario.Id, CriarDto(tipo.Id));
        await service.EnviarAsync(criado.Id);

        var aprovado = await service.AprovarAsync(criado.Id, aprovador.Id);

        Assert.Equal(ReembolsoDespesaStatus.Aprovado, aprovado.Status);
        Assert.Equal(aprovador.Id, aprovado.AprovadorId);
        Assert.Equal("Bruno", aprovado.AprovadorNome);
    }

    [Fact]
    public async Task AprovarAsync_DeveEnviarEmailComPdfAnexadoParaDestinatariosAtivos()
    {
        var (service, context, emailSender) = CriarService();
        var usuario = CriarUsuario(context, "Ana");
        var setor = CriarSetor(context);
        CompletarPerfil(context, usuario, setor.Id);
        var aprovador = CriarUsuario(context, "Bruno");
        TornarAprovador(context, setor.Id, aprovador.Id);
        var tipo = CriarTipoDespesa(context);
        var criado = await service.CreateAsync(usuario.Id, CriarDto(tipo.Id));
        await service.EnviarAsync(criado.Id);

        context.EmailsNotificacaoReembolso.Add(new EmailNotificacaoReembolso
        {
            Email = "financeiro@hope-br.com", TipoDestinatario = TipoDestinatarioEmail.Para, Ativo = true,
            DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime,
        });
        context.EmailsNotificacaoReembolso.Add(new EmailNotificacaoReembolso
        {
            Email = "controladoria@hope-br.com", TipoDestinatario = TipoDestinatarioEmail.Cc, Ativo = true,
            DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime,
        });
        context.EmailsNotificacaoReembolso.Add(new EmailNotificacaoReembolso
        {
            Email = "inativo@hope-br.com", TipoDestinatario = TipoDestinatarioEmail.Para, Ativo = false,
            DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime,
        });
        await context.SaveChangesAsync();

        await service.AprovarAsync(criado.Id, aprovador.Id);

        Assert.Equal(1, emailSender.ChamadasComAnexo);
        Assert.Equal(["financeiro@hope-br.com"], emailSender.UltimosDestinatarios);
        Assert.Equal(["controladoria@hope-br.com"], emailSender.UltimaCopia);
        var anexo = Assert.Single(emailSender.UltimosAnexos!);
        Assert.Equal("application/pdf", anexo.TipoConteudo);
        Assert.NotEmpty(anexo.Conteudo);
    }

    [Fact]
    public async Task AprovarAsync_DeveGerarPdfComLocalSelecionadoSemFalhar()
    {
        var (service, context, emailSender) = CriarService();
        var usuario = CriarUsuario(context, "Ana");
        var setor = CriarSetor(context);
        CompletarPerfil(context, usuario, setor.Id);
        var aprovador = CriarUsuario(context, "Bruno");
        TornarAprovador(context, setor.Id, aprovador.Id);
        var tipo = CriarTipoDespesa(context);
        var local = CriarLocal(context);

        var dto = CriarDto(tipo.Id);
        dto.LocalId = local.Id;
        var criado = await service.CreateAsync(usuario.Id, dto);
        await service.EnviarAsync(criado.Id);

        context.EmailsNotificacaoReembolso.Add(new EmailNotificacaoReembolso
        {
            Email = "financeiro@hope-br.com", TipoDestinatario = TipoDestinatarioEmail.Para, Ativo = true,
            DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime,
        });
        await context.SaveChangesAsync();

        var aprovado = await service.AprovarAsync(criado.Id, aprovador.Id);

        Assert.Equal(local.Id, aprovado.LocalId);
        Assert.Equal(1, emailSender.ChamadasComAnexo);
        var anexo = Assert.Single(emailSender.UltimosAnexos!);
        Assert.NotEmpty(anexo.Conteudo);

        var pdf = await service.GerarPdfAsync(criado.Id);
        Assert.NotEmpty(pdf);
    }

    [Fact]
    public async Task AprovarAsync_NaoDeveFalharQuandoNaoHaDestinatarioAtivo()
    {
        var (service, context, emailSender) = CriarService();
        var usuario = CriarUsuario(context, "Ana");
        var setor = CriarSetor(context);
        CompletarPerfil(context, usuario, setor.Id);
        var aprovador = CriarUsuario(context, "Bruno");
        TornarAprovador(context, setor.Id, aprovador.Id);
        var tipo = CriarTipoDespesa(context);
        var criado = await service.CreateAsync(usuario.Id, CriarDto(tipo.Id));
        await service.EnviarAsync(criado.Id);

        var aprovado = await service.AprovarAsync(criado.Id, aprovador.Id);

        Assert.Equal(ReembolsoDespesaStatus.Aprovado, aprovado.Status);
        Assert.Equal(0, emailSender.ChamadasComAnexo);
    }

    [Fact]
    public async Task AprovarAsync_DevePermitirAutoAprovacaoQuandoSolicitanteEhAprovadorDoProprioSetor()
    {
        var (service, context, _) = CriarService();
        var usuario = CriarUsuario(context, "Ana");
        var setor = CriarSetor(context);
        CompletarPerfil(context, usuario, setor.Id);
        TornarAprovador(context, setor.Id, usuario.Id);
        var tipo = CriarTipoDespesa(context);
        var criado = await service.CreateAsync(usuario.Id, CriarDto(tipo.Id));
        await service.EnviarAsync(criado.Id);

        var aprovado = await service.AprovarAsync(criado.Id, usuario.Id);

        Assert.Equal(ReembolsoDespesaStatus.Aprovado, aprovado.Status);
    }

    [Fact]
    public async Task AprovarAsync_DeveRejeitarQuandoUsuarioNaoEhAprovadorDoSetor()
    {
        var (service, context, _) = CriarService();
        var usuario = CriarUsuario(context, "Ana");
        var setor = CriarSetor(context);
        CompletarPerfil(context, usuario, setor.Id);
        var naoAprovador = CriarUsuario(context, "Carlos");
        var tipo = CriarTipoDespesa(context);
        var criado = await service.CreateAsync(usuario.Id, CriarDto(tipo.Id));
        await service.EnviarAsync(criado.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AprovarAsync(criado.Id, naoAprovador.Id));
    }

    [Fact]
    public async Task DevolverAsync_DeveExigirObservacaoEVoltarParaRevisao()
    {
        var (service, context, _) = CriarService();
        var usuario = CriarUsuario(context, "Ana");
        var setor = CriarSetor(context);
        CompletarPerfil(context, usuario, setor.Id);
        var aprovador = CriarUsuario(context, "Bruno");
        TornarAprovador(context, setor.Id, aprovador.Id);
        var tipo = CriarTipoDespesa(context);
        var criado = await service.CreateAsync(usuario.Id, CriarDto(tipo.Id));
        await service.EnviarAsync(criado.Id);

        var devolvido = await service.DevolverAsync(
            criado.Id, aprovador.Id, new DevolverReembolsoDespesaDto { ObservacaoAprovador = "Falta nota fiscal" });

        Assert.Equal(ReembolsoDespesaStatus.DevolvidoParaRevisao, devolvido.Status);
        Assert.Equal("Falta nota fiscal", devolvido.ObservacaoAprovador);

        // O reembolso volta a ser editável e reenviável após ser devolvido.
        var reenviado = await service.EnviarAsync(criado.Id);
        Assert.Equal(ReembolsoDespesaStatus.EnviadoParaAprovacao, reenviado.Status);
    }

    [Fact]
    public async Task ReprovarAsync_DeveMudarStatusParaReprovado()
    {
        var (service, context, _) = CriarService();
        var usuario = CriarUsuario(context, "Ana");
        var setor = CriarSetor(context);
        CompletarPerfil(context, usuario, setor.Id);
        var aprovador = CriarUsuario(context, "Bruno");
        TornarAprovador(context, setor.Id, aprovador.Id);
        var tipo = CriarTipoDespesa(context);
        var criado = await service.CreateAsync(usuario.Id, CriarDto(tipo.Id));
        await service.EnviarAsync(criado.Id);

        var reprovado = await service.ReprovarAsync(criado.Id, aprovador.Id, new ReprovarReembolsoDespesaDto());

        Assert.Equal(ReembolsoDespesaStatus.Reprovado, reprovado.Status);
    }

    [Fact]
    public async Task ExcluirAsync_DeveRejeitarReembolsoEnviadoParaAprovacao()
    {
        var (service, context, _) = CriarService();
        var usuario = CriarUsuario(context);
        var setor = CriarSetor(context);
        CompletarPerfil(context, usuario, setor.Id);
        var tipo = CriarTipoDespesa(context);
        var criado = await service.CreateAsync(usuario.Id, CriarDto(tipo.Id));
        await service.EnviarAsync(criado.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ExcluirAsync(criado.Id));
    }

    [Fact]
    public async Task GetPendentesParaAprovacaoAsync_DeveListarSomenteEnviadosDoSetorDoAprovador()
    {
        var (service, context, _) = CriarService();
        var usuarioA = CriarUsuario(context, "Ana");
        var setorA = CriarSetor(context, "Financeiro");
        CompletarPerfil(context, usuarioA, setorA.Id);

        var usuarioB = CriarUsuario(context, "Bia");
        var setorB = CriarSetor(context, "TI");
        CompletarPerfil(context, usuarioB, setorB.Id);

        var aprovador = CriarUsuario(context, "Carlos");
        TornarAprovador(context, setorA.Id, aprovador.Id);

        var tipo = CriarTipoDespesa(context);
        var deAna = await service.CreateAsync(usuarioA.Id, CriarDto(tipo.Id));
        await service.EnviarAsync(deAna.Id);
        var deBia = await service.CreateAsync(usuarioB.Id, CriarDto(tipo.Id));
        await service.EnviarAsync(deBia.Id);

        var pendentes = await service.GetPendentesParaAprovacaoAsync(aprovador.Id);

        var item = Assert.Single(pendentes);
        Assert.Equal(deAna.Id, item.Id);
    }

    [Fact]
    public async Task GetAprovadosPorMimAsync_DeveListarSomenteAprovadosPorEsteAprovadorOrdenadosPorDecisaoDesc()
    {
        var (service, context, _) = CriarService();
        var usuarioA = CriarUsuario(context, "Ana");
        var setor = CriarSetor(context);
        CompletarPerfil(context, usuarioA, setor.Id);
        var usuarioB = CriarUsuario(context, "Bia");
        CompletarPerfil(context, usuarioB, setor.Id);
        var aprovador = CriarUsuario(context, "Carlos");
        TornarAprovador(context, setor.Id, aprovador.Id);
        var outroAprovador = CriarUsuario(context, "Diego");
        TornarAprovador(context, setor.Id, outroAprovador.Id);
        var tipo = CriarTipoDespesa(context);

        var primeiro = await service.CreateAsync(usuarioA.Id, CriarDto(tipo.Id));
        await service.EnviarAsync(primeiro.Id);
        await service.AprovarAsync(primeiro.Id, aprovador.Id);

        // Serviço com um instante posterior, para o segundo aprovado ter DataDecisao mais recente.
        var configuracao = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var servicoInstanteSeguinte = new ReembolsoDespesaService(
            context, new FakeTimeProvider(Agora.AddMinutes(1)), NullLogger<ReembolsoDespesaService>.Instance,
            configuracao, new FakeEmailSender(), new AuditoriaService(context, new FakeTimeProvider(Agora.AddMinutes(1))));

        var segundo = await servicoInstanteSeguinte.CreateAsync(usuarioB.Id, CriarDto(tipo.Id));
        await servicoInstanteSeguinte.EnviarAsync(segundo.Id);
        await servicoInstanteSeguinte.AprovarAsync(segundo.Id, aprovador.Id);

        // Reprovado pelo mesmo aprovador não deve aparecer na lista de aprovados.
        var terceiro = await service.CreateAsync(usuarioA.Id, CriarDto(tipo.Id));
        await service.EnviarAsync(terceiro.Id);
        await service.ReprovarAsync(terceiro.Id, aprovador.Id, new ReprovarReembolsoDespesaDto());

        // Aprovado por outro aprovador não deve aparecer.
        var quarto = await service.CreateAsync(usuarioB.Id, CriarDto(tipo.Id));
        await service.EnviarAsync(quarto.Id);
        await service.AprovarAsync(quarto.Id, outroAprovador.Id);

        var resultado = await service.GetAprovadosPorMimAsync(aprovador.Id);

        Assert.Equal(2, resultado.Count);
        Assert.Equal(segundo.Id, resultado[0].Id);
        Assert.Equal(primeiro.Id, resultado[1].Id);
    }

    [Fact]
    public async Task GerarPdfAsync_DeveGerarArquivoNaoVazio()
    {
        var (service, context, _) = CriarService();
        var usuario = CriarUsuario(context);
        var setor = CriarSetor(context);
        CompletarPerfil(context, usuario, setor.Id);
        var tipo = CriarTipoDespesa(context);
        var criado = await service.CreateAsync(usuario.Id, CriarDto(tipo.Id, 199.90m));

        var pdf = await service.GerarPdfAsync(criado.Id);

        Assert.NotEmpty(pdf);
        // Assinatura de arquivo PDF (%PDF) no início do conteúdo.
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public async Task CreateAsync_DeveRegistrarLogDeAuditoria()
    {
        var (service, context, _) = CriarService();
        var usuario = CriarUsuario(context);
        var tipo = CriarTipoDespesa(context);

        var criado = await service.CreateAsync(usuario.Id, CriarDto(tipo.Id));

        var log = Assert.Single(context.LogsAuditoria);
        Assert.Equal(usuario.Id, log.UsuarioId);
        Assert.Equal(LogAuditoriaEntidade.ReembolsoDespesa, log.Entidade);
        Assert.Equal(criado.Id, log.EntidadeId);
        Assert.Equal(LogAuditoriaAcao.Criado, log.Acao);
    }

    [Fact]
    public async Task DevolverAsync_DeveRegistrarLogDeAuditoriaComMotivo()
    {
        var (service, context, _) = CriarService();
        var usuario = CriarUsuario(context, "Ana");
        var setor = CriarSetor(context);
        CompletarPerfil(context, usuario, setor.Id);
        var aprovador = CriarUsuario(context, "Bruno");
        TornarAprovador(context, setor.Id, aprovador.Id);
        var tipo = CriarTipoDespesa(context);
        var criado = await service.CreateAsync(usuario.Id, CriarDto(tipo.Id));
        await service.EnviarAsync(criado.Id);

        await service.DevolverAsync(criado.Id, aprovador.Id, new DevolverReembolsoDespesaDto { ObservacaoAprovador = "Falta comprovante" });

        var logDevolvido = context.LogsAuditoria.Single(l => l.Acao == LogAuditoriaAcao.Devolvido);
        Assert.Equal(aprovador.Id, logDevolvido.UsuarioId);
        Assert.Equal("Falta comprovante", logDevolvido.Detalhe);

        // O log de auditoria continua guardando o motivo da devolucao mesmo depois do reembolso
        // ser reenviado e aprovado - diferente do campo ObservacaoAprovador na entidade, que e sobrescrito.
        await service.EnviarAsync(criado.Id);
        await service.AprovarAsync(criado.Id, aprovador.Id);

        var aprovadoDto = await service.GetByIdAsync(criado.Id);
        Assert.Null(aprovadoDto.ObservacaoAprovador);

        var logDevolvidoAposAprovar = context.LogsAuditoria.Single(l => l.Acao == LogAuditoriaAcao.Devolvido);
        Assert.Equal("Falta comprovante", logDevolvidoAposAprovar.Detalhe);
    }

    [Fact]
    public async Task ExcluirAsync_ChamadoPeloAdministrador_DeveRegistrarLogSemUsuarioId()
    {
        var (service, context, _) = CriarService();
        var usuario = CriarUsuario(context);
        var tipo = CriarTipoDespesa(context);
        var criado = await service.CreateAsync(usuario.Id, CriarDto(tipo.Id));

        await service.ExcluirAsync(criado.Id, usuarioIdAtor: null);

        var log = context.LogsAuditoria.Single(l => l.Acao == LogAuditoriaAcao.Excluido);
        Assert.Null(log.UsuarioId);
        Assert.Equal("Administrador", log.UsuarioNome);
    }

    private static AdicionarAnexoDto CriarAnexoDto(string nomeArquivo = "comprovante.jpg") => new()
    {
        NomeArquivo = nomeArquivo,
        TipoConteudo = "image/jpeg",
        Conteudo = [1, 2, 3],
    };

    [Fact]
    public async Task UpdateAsync_DevePreservarIdEComprovanteDoItemExistente()
    {
        var (service, context, _) = CriarService();
        var usuario = CriarUsuario(context);
        var tipo = CriarTipoDespesa(context);
        var criado = await service.CreateAsync(usuario.Id, CriarDto(tipo.Id, 100m));
        var itemId = criado.Itens[0].Id;
        await service.AdicionarAnexoItemAsync(criado.Id, itemId, CriarAnexoDto());

        var atualizado = await service.UpdateAsync(criado.Id, new UpdateReembolsoDespesaDto
        {
            Finalidade = "Viagem a cliente - atualizado",
            Itens = [new CreateReembolsoDespesaItemDto { Id = itemId, Data = Hoje, TipoDespesaId = tipo.Id, Valor = 250m }],
        });

        var item = Assert.Single(atualizado.Itens);
        Assert.Equal(itemId, item.Id);
        Assert.Equal(250m, item.Valor);
        var anexo = Assert.Single(item.Anexos);
        Assert.Equal("comprovante.jpg", anexo.NomeArquivo);
    }

    [Fact]
    public async Task UpdateAsync_DeveExcluirComprovanteDeItemRemovido()
    {
        var (service, context, _) = CriarService();
        var usuario = CriarUsuario(context);
        var tipo = CriarTipoDespesa(context);
        var criado = await service.CreateAsync(usuario.Id, CriarDto(tipo.Id, 100m));
        var itemId = criado.Itens[0].Id;
        await service.AdicionarAnexoItemAsync(criado.Id, itemId, CriarAnexoDto());

        var outroTipo = CriarTipoDespesa(context, "Hospedagem");
        await service.UpdateAsync(criado.Id, new UpdateReembolsoDespesaDto
        {
            Finalidade = "Item trocado",
            Itens = [new CreateReembolsoDespesaItemDto { Data = Hoje, TipoDespesaId = outroTipo.Id, Valor = 300m }],
        });

        Assert.Empty(await context.ReembolsoDespesaItemAnexos.Where(a => a.ReembolsoDespesaItemId == itemId).ToListAsync());
    }

    [Fact]
    public async Task ListarAnexosItemAsync_DeveRetornarAnexosDoItem()
    {
        var (service, context, _) = CriarService();
        var usuario = CriarUsuario(context);
        var tipo = CriarTipoDespesa(context);
        var criado = await service.CreateAsync(usuario.Id, CriarDto(tipo.Id));
        var itemId = criado.Itens[0].Id;
        await service.AdicionarAnexoItemAsync(criado.Id, itemId, CriarAnexoDto());

        var lista = await service.ListarAnexosItemAsync(criado.Id, itemId);

        var anexo = Assert.Single(lista);
        Assert.Equal("comprovante.jpg", anexo.NomeArquivo);
    }

    [Fact]
    public async Task AdicionarAnexoItemAsync_DeveSalvarERegistrarAuditoria()
    {
        var (service, context, _) = CriarService();
        var usuario = CriarUsuario(context);
        var tipo = CriarTipoDespesa(context);
        var criado = await service.CreateAsync(usuario.Id, CriarDto(tipo.Id));
        var itemId = criado.Itens[0].Id;

        var anexo = await service.AdicionarAnexoItemAsync(criado.Id, itemId, CriarAnexoDto(), usuario.Id);

        Assert.Equal("comprovante.jpg", anexo.NomeArquivo);
        var log = context.LogsAuditoria.Single(l => l.Acao == LogAuditoriaAcao.AnexoAdicionado);
        Assert.Equal(usuario.Id, log.UsuarioId);
    }

    [Fact]
    public async Task AdicionarAnexoItemAsync_DeveRejeitarTipoDeArquivoInvalido()
    {
        var (service, context, _) = CriarService();
        var usuario = CriarUsuario(context);
        var tipo = CriarTipoDespesa(context);
        var criado = await service.CreateAsync(usuario.Id, CriarDto(tipo.Id));
        var itemId = criado.Itens[0].Id;

        var dto = CriarAnexoDto();
        dto.TipoConteudo = "application/zip";

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AdicionarAnexoItemAsync(criado.Id, itemId, dto));
    }

    [Fact]
    public async Task AdicionarAnexoItemAsync_DeveRejeitarQuandoStatusNaoEditavel()
    {
        var (service, context, _) = CriarService();
        var usuario = CriarUsuario(context);
        var setor = CriarSetor(context);
        CompletarPerfil(context, usuario, setor.Id);
        var tipo = CriarTipoDespesa(context);
        var criado = await service.CreateAsync(usuario.Id, CriarDto(tipo.Id));
        var itemId = criado.Itens[0].Id;
        await service.EnviarAsync(criado.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AdicionarAnexoItemAsync(criado.Id, itemId, CriarAnexoDto()));
    }

    [Fact]
    public async Task ObterAnexoItemAsync_DeveRetornarConteudo()
    {
        var (service, context, _) = CriarService();
        var usuario = CriarUsuario(context);
        var tipo = CriarTipoDespesa(context);
        var criado = await service.CreateAsync(usuario.Id, CriarDto(tipo.Id));
        var itemId = criado.Itens[0].Id;
        var anexo = await service.AdicionarAnexoItemAsync(criado.Id, itemId, CriarAnexoDto());

        var arquivo = await service.ObterAnexoItemAsync(criado.Id, itemId, anexo.Id);

        Assert.Equal("comprovante.jpg", arquivo.NomeArquivo);
        Assert.Equal<byte[]>([1, 2, 3], arquivo.Conteudo);
    }

    [Fact]
    public async Task ExcluirAnexoItemAsync_DeveRemoverERegistrarAuditoria()
    {
        var (service, context, _) = CriarService();
        var usuario = CriarUsuario(context);
        var tipo = CriarTipoDespesa(context);
        var criado = await service.CreateAsync(usuario.Id, CriarDto(tipo.Id));
        var itemId = criado.Itens[0].Id;
        var anexo = await service.AdicionarAnexoItemAsync(criado.Id, itemId, CriarAnexoDto());

        await service.ExcluirAnexoItemAsync(criado.Id, itemId, anexo.Id, usuario.Id);

        Assert.Empty(await context.ReembolsoDespesaItemAnexos.Where(a => a.ReembolsoDespesaItemId == itemId).ToListAsync());
        Assert.Contains(context.LogsAuditoria, l => l.Acao == LogAuditoriaAcao.AnexoExcluido);
    }

    [Fact]
    public async Task EhAprovadorDoSetorAsync_DeveDistinguirAprovadorDeNaoAprovador()
    {
        var (service, context, _) = CriarService();
        var setor = CriarSetor(context);
        var aprovador = CriarUsuario(context, "Bruno");
        var naoAprovador = CriarUsuario(context, "Carlos");
        TornarAprovador(context, setor.Id, aprovador.Id);

        Assert.True(await service.EhAprovadorDoSetorAsync(aprovador.Id, setor.Id));
        Assert.False(await service.EhAprovadorDoSetorAsync(naoAprovador.Id, setor.Id));
        Assert.False(await service.EhAprovadorDoSetorAsync(aprovador.Id, null));
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

// Lançamento mensal do plano de saúde mostra, por usuário, a coparticipação total gravada e a nota de débito do mês.
public class PlanoSaudeNotaDebitoMesTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    private static (PlanoSaudeCustoService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var context = new AppDbContext(options);
        return (new PlanoSaudeCustoService(context, new FakeTimeProvider(Agora), NullLogger<PlanoSaudeCustoService>.Instance), context);
    }

    private static Usuario CriarUsuario(AppDbContext context, string nome, string tipo = UsuarioTipo.Pj)
    {
        var usuario = new Usuario
        {
            Nome = nome, Email = $"{nome.ToLowerInvariant()}@empresa.com", DataInicio = new DateOnly(2020, 1, 1), Tipo = tipo,
            DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime,
        };
        context.Usuarios.Add(usuario);
        context.SaveChanges();
        return usuario;
    }

    private static void Lancar(AppDbContext context, int usuarioId, decimal coparticipacao, int? dependenteId = null, int mes = 8)
    {
        context.PlanoSaudeCustos.Add(new PlanoSaudeCusto
        {
            UsuarioId = usuarioId, DependenteId = dependenteId, Ano = 2026, Mes = mes, ValorMensal = 500, ValorCoparticipacao = coparticipacao,
            DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime,
        });
        context.SaveChanges();
    }

    private static void CriarNota(AppDbContext context, int usuarioId, decimal valor, string status, int mes = 8)
    {
        context.NotasDebitoPj.Add(new NotaDebitoPj
        {
            UsuarioId = usuarioId, Ano = 2026, Mes = mes, ValorBruto = valor, OperadoraSaude = "Operadora", Status = status,
            DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime,
        });
        context.SaveChanges();
    }

    [Fact]
    public async Task GetMesAsync_DeveSomarCoparticipacaoDoTitularEDosDependentesSemNota()
    {
        var (service, context) = CriarService();
        var usuario = CriarUsuario(context, "Ana");
        var dependente = new Dependente { UsuarioId = usuario.Id, Nome = "Filho", Ativo = true, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        context.Dependentes.Add(dependente);
        context.SaveChanges();
        Lancar(context, usuario.Id, 100m);
        Lancar(context, usuario.Id, 40.5m, dependenteId: dependente.Id);

        var mes = await service.GetMesAsync(new PlanoSaudeMesFiltroDto { Ano = 2026, Mes = 8 });

        var linha = Assert.Single(mes.Usuarios);
        Assert.Equal(140.5m, linha.CoparticipacaoTotal);
        Assert.Null(linha.NotaDebitoId);
        Assert.Null(linha.NotaDebitoStatus);
        Assert.Null(linha.NotaDebitoValor);
    }

    [Fact]
    public async Task GetMesAsync_DeveTrazerANotaDebitoDoMesDoUsuario()
    {
        var (service, context) = CriarService();
        var ana = CriarUsuario(context, "Ana");
        var bia = CriarUsuario(context, "Bia");
        Lancar(context, ana.Id, 100m);
        Lancar(context, bia.Id, 80m);
        CriarNota(context, ana.Id, 100m, NotaDebitoPjStatus.Enviada);
        // Notas de outro mês não contam para agosto.
        CriarNota(context, bia.Id, 80m, NotaDebitoPjStatus.Rascunho, mes: 7);

        var mes = await service.GetMesAsync(new PlanoSaudeMesFiltroDto { Ano = 2026, Mes = 8 });

        var linhaAna = mes.Usuarios.Single(u => u.UsuarioId == ana.Id);
        Assert.NotNull(linhaAna.NotaDebitoId);
        Assert.Equal(NotaDebitoPjStatus.Enviada, linhaAna.NotaDebitoStatus);
        Assert.Equal(100m, linhaAna.NotaDebitoValor);
        var linhaBia = mes.Usuarios.Single(u => u.UsuarioId == bia.Id);
        Assert.Null(linhaBia.NotaDebitoId);
    }

    [Fact]
    public async Task GetMesAsync_ValorDaNotaFicaCongeladoQuandoACoparticipacaoMuda()
    {
        var (service, context) = CriarService();
        var ana = CriarUsuario(context, "Ana");
        Lancar(context, ana.Id, 150m);
        CriarNota(context, ana.Id, 100m, NotaDebitoPjStatus.Rascunho);

        var linha = Assert.Single((await service.GetMesAsync(new PlanoSaudeMesFiltroDto { Ano = 2026, Mes = 8 })).Usuarios);

        // A tela usa essa diferença para marcar a nota como divergente.
        Assert.Equal(150m, linha.CoparticipacaoTotal);
        Assert.Equal(100m, linha.NotaDebitoValor);
    }
}

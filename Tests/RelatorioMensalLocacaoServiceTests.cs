using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class RelatorioMensalLocacaoServiceTests
{
    private static readonly DateTime Agora = new(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc);

    private static (RelatorioMensalLocacaoService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        return (new RelatorioMensalLocacaoService(context), context);
    }

    private static TipoEquipamento CriarTipo(AppDbContext context, string nome = "Notebook")
    {
        var tipo = new TipoEquipamento { Nome = nome, Ativo = true, DataCriacao = Agora, DataAtualizacao = Agora };
        context.TiposEquipamento.Add(tipo);
        context.SaveChanges();
        return tipo;
    }

    private static Equipamento CriarEquipamentoLocado(
        AppDbContext context,
        TipoEquipamento tipo,
        DateOnly dataInicioContrato,
        DateOnly? dataFimContrato,
        decimal valorMensal = 300m,
        string? fornecedorNome = null)
    {
        var equipamento = new Equipamento
        {
            TipoEquipamentoId = tipo.Id,
            Origem = EquipamentoOrigem.Locado,
            ValorMensal = valorMensal,
            DataInicioContrato = dataInicioContrato,
            DataFimContrato = dataFimContrato,
            FornecedorNome = fornecedorNome,
            Status = EquipamentoStatus.Disponivel,
            DataCriacao = Agora,
            DataAtualizacao = Agora,
        };
        context.Equipamentos.Add(equipamento);
        context.SaveChanges();
        return equipamento;
    }

    [Fact]
    public async Task GerarAsync_DeveCobrarValorIntegralQuandoAtivoOMesInteiro()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipo(context);
        CriarEquipamentoLocado(context, tipo, new DateOnly(2026, 1, 1), null, valorMensal: 300m);

        var relatorio = await service.GerarAsync(new RelatorioMensalLocacaoFiltroDto { Ano = 2026, Mes = 8 });

        var item = Assert.Single(relatorio.Itens);
        Assert.Equal(31, item.DiasNoMes);
        Assert.Equal(31, item.DiasAtivos);
        Assert.Equal(300m, item.ValorNoMes);
        Assert.Equal(300m, relatorio.TotalGeral);
    }

    [Fact]
    public async Task GerarAsync_DeveRatearQuandoContratoComecaNoMeioDoMes()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipo(context);
        // Agosto tem 31 dias; começando dia 22, ativo 10 dias (22 a 31).
        CriarEquipamentoLocado(context, tipo, new DateOnly(2026, 8, 22), null, valorMensal: 310m);

        var relatorio = await service.GerarAsync(new RelatorioMensalLocacaoFiltroDto { Ano = 2026, Mes = 8 });

        var item = Assert.Single(relatorio.Itens);
        Assert.Equal(10, item.DiasAtivos);
        Assert.Equal(100m, item.ValorNoMes);
    }

    [Fact]
    public async Task GerarAsync_DeveRatearQuandoContratoTerminaNoMeioDoMes()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipo(context);
        // Contrato termina dia 10/08 -> ativo do dia 1 ao 10 = 10 dias de 31.
        CriarEquipamentoLocado(context, tipo, new DateOnly(2026, 1, 1), new DateOnly(2026, 8, 10), valorMensal: 310m);

        var relatorio = await service.GerarAsync(new RelatorioMensalLocacaoFiltroDto { Ano = 2026, Mes = 8 });

        var item = Assert.Single(relatorio.Itens);
        Assert.Equal(10, item.DiasAtivos);
        Assert.Equal(100m, item.ValorNoMes);
    }

    [Fact]
    public async Task GerarAsync_DeveTrazerDataDeChegadaDaNotaFiscal()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipo(context);

        var nota = new NotaFiscalEntrada { Numero = "1", DataEntrada = new DateOnly(2026, 2, 13), DataCriacao = Agora, DataAtualizacao = Agora };
        context.NotasFiscaisEntrada.Add(nota);
        context.SaveChanges();

        var item = new NotaFiscalItem { NotaFiscalEntradaId = nota.Id, Destino = NotaFiscalItemDestino.Equipamento, TipoEquipamentoId = tipo.Id, Quantidade = 1, Origem = EquipamentoOrigem.Locado, DataCriacao = Agora };
        context.NotasFiscaisItens.Add(item);
        context.SaveChanges();

        context.Equipamentos.Add(new Equipamento
        {
            TipoEquipamentoId = tipo.Id,
            NotaFiscalItemId = item.Id,
            Origem = EquipamentoOrigem.Locado,
            ValorMensal = 100m,
            DataInicioContrato = new DateOnly(2026, 1, 1),
            Status = EquipamentoStatus.Disponivel,
            DataCriacao = Agora,
            DataAtualizacao = Agora,
        });
        context.SaveChanges();

        var relatorio = await service.GerarAsync(new RelatorioMensalLocacaoFiltroDto { Ano = 2026, Mes = 8 });

        var itemRelatorio = Assert.Single(relatorio.Itens);
        Assert.Equal(new DateOnly(2026, 2, 13), itemRelatorio.DataChegada);
    }

    [Fact]
    public async Task GerarAsync_DeveTrazerUsuarioResponsavelEOrdenarPorEle()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipo(context);

        var equipamentoSemUsuario = CriarEquipamentoLocado(context, tipo, new DateOnly(2026, 1, 1), null, valorMensal: 100m);
        var equipamentoJoao = CriarEquipamentoLocado(context, tipo, new DateOnly(2026, 1, 1), null, valorMensal: 100m);
        var equipamentoAna = CriarEquipamentoLocado(context, tipo, new DateOnly(2026, 1, 1), null, valorMensal: 100m);

        var joao = new Usuario { Nome = "João Silva", Email = "joao@empresa.com", DataInicio = new DateOnly(2025, 1, 1), DataCriacao = Agora, DataAtualizacao = Agora };
        var ana = new Usuario { Nome = "Ana Costa", Email = "ana@empresa.com", DataInicio = new DateOnly(2025, 1, 1), DataCriacao = Agora, DataAtualizacao = Agora };
        context.Usuarios.AddRange(joao, ana);
        context.SaveChanges();

        context.EquipamentoAlocacoes.AddRange(
            new EquipamentoAlocacao { EquipamentoId = equipamentoJoao.Id, UsuarioId = joao.Id, DataInicio = new DateOnly(2026, 1, 1), DataCriacao = Agora, DataAtualizacao = Agora },
            new EquipamentoAlocacao { EquipamentoId = equipamentoAna.Id, UsuarioId = ana.Id, DataInicio = new DateOnly(2026, 1, 1), DataCriacao = Agora, DataAtualizacao = Agora });
        context.SaveChanges();

        var relatorio = await service.GerarAsync(new RelatorioMensalLocacaoFiltroDto { Ano = 2026, Mes = 8 });

        Assert.Equal(3, relatorio.Itens.Count);
        // Ana antes de João (ordem alfabética), sem usuário por último.
        Assert.Equal(equipamentoAna.Id, relatorio.Itens[0].EquipamentoId);
        Assert.Equal("Ana Costa", relatorio.Itens[0].UsuarioResponsavelNome);
        Assert.Equal("ana@empresa.com", relatorio.Itens[0].UsuarioResponsavelEmail);
        Assert.Equal(equipamentoJoao.Id, relatorio.Itens[1].EquipamentoId);
        Assert.Equal(equipamentoSemUsuario.Id, relatorio.Itens[2].EquipamentoId);
        Assert.Null(relatorio.Itens[2].UsuarioResponsavelNome);
        Assert.Null(relatorio.Itens[2].UsuarioResponsavelEmail);
    }

    [Fact]
    public async Task GerarAsync_DeveIncluirEquipamentoLocadoSemValorLancadoComoZero()
    {
        // Achado real: um equipamento locado sem valor de faturamento lançado (ValorMensal null)
        // sumia do relatório, enquanto outro com ValorMensal = 0 explícito aparecia normalmente.
        var (service, context) = CriarService();
        var tipo = CriarTipo(context);
        context.Equipamentos.Add(new Equipamento
        {
            TipoEquipamentoId = tipo.Id,
            Origem = EquipamentoOrigem.Locado,
            ValorMensal = null,
            DataInicioContrato = new DateOnly(2026, 1, 1),
            DataFimContrato = null,
            Status = EquipamentoStatus.Disponivel,
            DataCriacao = Agora,
            DataAtualizacao = Agora,
        });
        context.SaveChanges();

        var relatorio = await service.GerarAsync(new RelatorioMensalLocacaoFiltroDto { Ano = 2026, Mes = 8 });

        var item = Assert.Single(relatorio.Itens);
        Assert.Equal(0m, item.ValorMensal);
        Assert.Equal(0m, item.ValorNoMes);
    }

    [Fact]
    public async Task GerarAsync_NaoDeveIncluirEquipamentoComContratoEncerradoAntesDoMes()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipo(context);
        CriarEquipamentoLocado(context, tipo, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));

        var relatorio = await service.GerarAsync(new RelatorioMensalLocacaoFiltroDto { Ano = 2026, Mes = 8 });

        Assert.Empty(relatorio.Itens);
        Assert.Equal(0m, relatorio.TotalGeral);
    }

    [Fact]
    public async Task GerarAsync_NaoDeveIncluirEquipamentoComContratoAindaNaoIniciado()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipo(context);
        CriarEquipamentoLocado(context, tipo, new DateOnly(2026, 9, 1), null);

        var relatorio = await service.GerarAsync(new RelatorioMensalLocacaoFiltroDto { Ano = 2026, Mes = 8 });

        Assert.Empty(relatorio.Itens);
    }

    [Fact]
    public async Task GerarAsync_NaoDeveIncluirEquipamentoComprado()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipo(context);
        var equipamento = new Equipamento
        {
            TipoEquipamentoId = tipo.Id,
            Origem = EquipamentoOrigem.Comprado,
            DataInicioContrato = new DateOnly(2026, 1, 1),
            Status = EquipamentoStatus.Disponivel,
            DataCriacao = Agora,
            DataAtualizacao = Agora,
        };
        context.Equipamentos.Add(equipamento);
        await context.SaveChangesAsync();

        var relatorio = await service.GerarAsync(new RelatorioMensalLocacaoFiltroDto { Ano = 2026, Mes = 8 });

        Assert.Empty(relatorio.Itens);
    }

    [Fact]
    public async Task GerarAsync_DeveSomarTotalGeralDeMultiplosEquipamentos()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipo(context);
        CriarEquipamentoLocado(context, tipo, new DateOnly(2026, 1, 1), null, valorMensal: 300m);
        CriarEquipamentoLocado(context, tipo, new DateOnly(2026, 1, 1), null, valorMensal: 200m);

        var relatorio = await service.GerarAsync(new RelatorioMensalLocacaoFiltroDto { Ano = 2026, Mes = 8 });

        Assert.Equal(2, relatorio.Itens.Count);
        Assert.Equal(500m, relatorio.TotalGeral);
    }

    [Fact]
    public async Task GerarAsync_DeveRejeitarMesInvalido()
    {
        var (service, _) = CriarService();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.GerarAsync(new RelatorioMensalLocacaoFiltroDto { Ano = 2026, Mes = 13 }));
    }

    [Fact]
    public async Task GerarExcel_DeveGerarArquivoNaoVazio()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipo(context);
        CriarEquipamentoLocado(context, tipo, new DateOnly(2026, 1, 1), null, valorMensal: 300m);
        var relatorio = await service.GerarAsync(new RelatorioMensalLocacaoFiltroDto { Ano = 2026, Mes = 8 });

        var arquivo = service.GerarExcel(relatorio);

        Assert.NotEmpty(arquivo);
    }
}

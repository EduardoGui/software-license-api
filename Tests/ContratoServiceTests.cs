using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class ContratoServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);

    private static (ContratoService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var service = new ContratoService(context, new FakeTimeProvider(Agora), NullLogger<ContratoService>.Instance);
        return (service, context);
    }

    private static Fornecedor CriarFornecedor(AppDbContext context, string nome = "SPAZI Arq design")
    {
        var fornecedor = new Fornecedor { Nome = nome, Ativo = true, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        context.Fornecedores.Add(fornecedor);
        context.SaveChanges();
        return fornecedor;
    }

    private static CreateContratoDto CriarDtoValido(int fornecedorId, string numero = "SUB_HOPE_0001_2026") => new()
    {
        Numero = numero,
        FornecedorId = fornecedorId,
        Objeto = "Prestação de serviços de consultoria",
        DataAssinatura = new DateOnly(2026, 1, 1),
        DataInicioVigencia = new DateOnly(2026, 1, 1),
        DataFimVigenciaOriginal = new DateOnly(2026, 12, 31),
        ValorOriginal = 165000m,
        Itens =
        [
            new CreateContratoItemDto { Descricao = "Serviço de consultoria", Unidade = "VB", QuantidadeContratada = 12m, ValorUnitario = 13750m },
        ],
        MedicaoConfig = new CreateContratoMedicaoConfigDto { TipoMedicao = SoftwareLicense.Api.Services.TipoMedicao.MensalFixo, PermiteProRata = true, MetodoProRata = SoftwareLicense.Api.Services.MetodoProRata.DiasCorridos },
    };

    [Fact]
    public async Task CreateAsync_DeveCriarContratoComItensEConfigs()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);

        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));

        Assert.Equal("SUB_HOPE_0001_2026", contrato.Numero);
        Assert.Equal(165000m, contrato.ValorOriginal);
        Assert.Equal(165000m, contrato.ValorAtual);
        Assert.Equal(new DateOnly(2026, 12, 31), contrato.DataFimVigenciaAtual);
        Assert.Equal(SoftwareLicense.Api.Services.ContratoStatus.Ativo, contrato.Status);
        Assert.Equal(1, contrato.QuantidadeItens);

        var detalhe = await service.GetByIdAsync(contrato.Id);
        Assert.Single(detalhe.Itens);
        Assert.Equal(165000m, detalhe.Itens[0].ValorTotal);
        Assert.NotNull(detalhe.MedicaoConfig);
        Assert.NotNull(detalhe.FaturamentoConfig);
        Assert.Equal(1, detalhe.FaturamentoConfig!.DiaInicialJanelaNf);
        Assert.Equal(24, detalhe.FaturamentoConfig!.DiaFinalJanelaNf);
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarFornecedorInexistente()
    {
        var (service, _) = CriarService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAsync(CriarDtoValido(999)));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarNumeroDuplicado()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        await service.CreateAsync(CriarDtoValido(fornecedor.Id));

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(CriarDtoValido(fornecedor.Id)));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarVigenciaInvalida()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var dto = CriarDtoValido(fornecedor.Id);
        dto.DataFimVigenciaOriginal = dto.DataInicioVigencia;

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarSemItens()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var dto = CriarDtoValido(fornecedor.Id);
        dto.Itens.Clear();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarTipoMedicaoInvalido()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var dto = CriarDtoValido(fornecedor.Id);
        dto.MedicaoConfig.TipoMedicao = "Inexistente";

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(dto));
    }

    [Fact]
    public async Task GetAllAsync_DeveFiltrarPorStatusEFornecedor()
    {
        var (service, context) = CriarService();
        var fornecedorA = CriarFornecedor(context, "Fornecedor A");
        var fornecedorB = CriarFornecedor(context, "Fornecedor B");
        var contratoA = await service.CreateAsync(CriarDtoValido(fornecedorA.Id, "NUM-A"));
        await service.CreateAsync(CriarDtoValido(fornecedorB.Id, "NUM-B"));

        var resultado = await service.GetAllAsync(new ContratoFiltroDto { FornecedorId = fornecedorA.Id });

        Assert.Single(resultado);
        Assert.Equal(contratoA.Id, resultado[0].Id);
    }

    [Fact]
    public async Task GetAllAsync_DeveFiltrarPorVigenciaFimAte()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var dtoQueVence = CriarDtoValido(fornecedor.Id, "NUM-VENCE-CEDO");
        dtoQueVence.DataFimVigenciaOriginal = new DateOnly(2026, 6, 30);
        var contratoQueVence = await service.CreateAsync(dtoQueVence);
        await service.CreateAsync(CriarDtoValido(fornecedor.Id, "NUM-VENCE-TARDE"));

        var resultado = await service.GetAllAsync(new ContratoFiltroDto { VigenciaFimAte = new DateOnly(2026, 7, 1) });

        Assert.Single(resultado);
        Assert.Equal(contratoQueVence.Id, resultado[0].Id);
    }

    [Fact]
    public async Task GetByIdAsync_DeveLancarNotFoundParaContratoInexistente()
    {
        var (service, _) = CriarService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(999));
    }

    [Fact]
    public async Task UpdateAsync_DeveAtualizarCamposAdministrativos()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));

        var atualizado = await service.UpdateAsync(contrato.Id, new UpdateContratoDto
        {
            Objeto = "Objeto revisado",
            Status = SoftwareLicense.Api.Services.ContratoStatus.Suspenso,
            Observacoes = "Suspenso temporariamente",
        });

        Assert.Equal("Objeto revisado", atualizado.Objeto);
        Assert.Equal(SoftwareLicense.Api.Services.ContratoStatus.Suspenso, atualizado.Status);
    }

    [Fact]
    public async Task UpdateAsync_DeveRejeitarStatusInvalido()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UpdateAsync(contrato.Id, new UpdateContratoDto { Objeto = "X", Status = "Inexistente" }));
    }

    [Fact]
    public async Task AtualizarFaturamentoConfigAsync_DeveRejeitarJanelaInvalida()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.AtualizarFaturamentoConfigAsync(contrato.Id, new UpdateContratoFaturamentoConfigDto { DiaInicialJanelaNf = 24, DiaFinalJanelaNf = 1 }));
    }

    [Fact]
    public async Task AtualizarMedicaoConfigAsync_DeveAtualizar()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));

        var atualizado = await service.AtualizarMedicaoConfigAsync(contrato.Id, new UpdateContratoMedicaoConfigDto
        {
            TipoMedicao = SoftwareLicense.Api.Services.TipoMedicao.ParcelaUnica,
            ExigeBm = true,
        });

        Assert.Equal(SoftwareLicense.Api.Services.TipoMedicao.ParcelaUnica, atualizado.TipoMedicao);
        Assert.True(atualizado.ExigeBm);
    }

    [Fact]
    public async Task AdicionarAnexoAsync_DeveSalvarAnexoValido()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));

        var anexo = await service.AdicionarAnexoAsync(contrato.Id, new AdicionarAnexoDto
        {
            NomeArquivo = "contrato.pdf",
            TipoConteudo = "application/pdf",
            Conteudo = [1, 2, 3],
        });

        Assert.Equal("contrato.pdf", anexo.NomeArquivo);

        var lista = await service.ListarAnexosAsync(contrato.Id);
        Assert.Single(lista);
    }

    [Fact]
    public async Task ExcluirAnexoAsync_DeveRemoverAnexo()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));
        var anexo = await service.AdicionarAnexoAsync(contrato.Id, new AdicionarAnexoDto
        {
            NomeArquivo = "contrato.pdf",
            TipoConteudo = "application/pdf",
            Conteudo = [1, 2, 3],
        });

        await service.ExcluirAnexoAsync(contrato.Id, anexo.Id);

        var lista = await service.ListarAnexosAsync(contrato.Id);
        Assert.Empty(lista);
    }

    private static CreateAditivoDto CriarAditivoDtoValido() => new()
    {
        Descricao = "Reajuste anual",
        DataAssinatura = new DateOnly(2026, 12, 1),
        DataEfeito = new DateOnly(2027, 1, 1),
    };

    [Fact]
    public async Task CriarAditivoAsync_DeveNascerComoPrevistoENaoAlterarValorAtual()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));

        var dto = CriarAditivoDtoValido();
        dto.DeltaValor = 10000m;
        var aditivo = await service.CriarAditivoAsync(contrato.Id, dto);

        Assert.Equal(AditivoStatus.Previsto, aditivo.Status);
        Assert.Equal(1, aditivo.Numero);

        var detalhe = await service.GetByIdAsync(contrato.Id);
        Assert.Equal(165000m, detalhe.ValorAtual);
    }

    [Fact]
    public async Task CriarAditivoAsync_DeveIncrementarNumeroSequencialPorContrato()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));

        var primeiro = await service.CriarAditivoAsync(contrato.Id, CriarAditivoDtoValido());
        var segundo = await service.CriarAditivoAsync(contrato.Id, CriarAditivoDtoValido());

        Assert.Equal(1, primeiro.Numero);
        Assert.Equal(2, segundo.Numero);
    }

    [Fact]
    public async Task CriarAditivoAsync_DeveRejeitarItemNovoSemDadosObrigatorios()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));
        var dto = CriarAditivoDtoValido();
        dto.Itens.Add(new CreateAditivoItemDto { DeltaQuantidade = 5m });

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CriarAditivoAsync(contrato.Id, dto));
    }

    [Fact]
    public async Task CriarAditivoAsync_DeveRejeitarContratoItemIdDeOutroContrato()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contratoA = await service.CreateAsync(CriarDtoValido(fornecedor.Id, "NUM-A"));
        await service.CreateAsync(CriarDtoValido(fornecedor.Id, "NUM-B"));
        var itemDeOutroContrato = await context.ContratoItens.FirstAsync(i => i.ContratoId != contratoA.Id);

        var dto = CriarAditivoDtoValido();
        dto.Itens.Add(new CreateAditivoItemDto { ContratoItemId = itemDeOutroContrato.Id, DeltaQuantidade = 1m });

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CriarAditivoAsync(contratoA.Id, dto));
    }

    [Fact]
    public async Task FormalizarAditivoAsync_DeveAtualizarValorEVigenciaAtualDoContrato()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));

        var dto = CriarAditivoDtoValido();
        dto.DeltaValor = 10000m;
        dto.NovaDataFimVigencia = new DateOnly(2027, 6, 30);
        var aditivo = await service.CriarAditivoAsync(contrato.Id, dto);

        var formalizado = await service.FormalizarAditivoAsync(contrato.Id, aditivo.Id);
        Assert.Equal(AditivoStatus.Formalizado, formalizado.Status);
        Assert.NotNull(formalizado.DataFormalizacao);

        var detalhe = await service.GetByIdAsync(contrato.Id);
        Assert.Equal(175000m, detalhe.ValorAtual);
        Assert.Equal(new DateOnly(2027, 6, 30), detalhe.DataFimVigenciaAtual);
    }

    [Fact]
    public async Task FormalizarAditivoAsync_ComPercentualReajuste_DeveCalcularSobreValorOriginal()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));

        var dto = CriarAditivoDtoValido();
        dto.PercentualReajuste = 10m;
        var aditivo = await service.CriarAditivoAsync(contrato.Id, dto);
        await service.FormalizarAditivoAsync(contrato.Id, aditivo.Id);

        var detalhe = await service.GetByIdAsync(contrato.Id);
        Assert.Equal(181500m, detalhe.ValorAtual);
    }

    [Fact]
    public async Task FormalizarAditivoAsync_DeveRejeitarFormalizarDuasVezes()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));
        var aditivo = await service.CriarAditivoAsync(contrato.Id, CriarAditivoDtoValido());
        await service.FormalizarAditivoAsync(contrato.Id, aditivo.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.FormalizarAditivoAsync(contrato.Id, aditivo.Id));
    }

    [Fact]
    public async Task ListarAditivosAsync_DeveRetornarEmOrdemDeNumero()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = await service.CreateAsync(CriarDtoValido(fornecedor.Id));
        await service.CriarAditivoAsync(contrato.Id, CriarAditivoDtoValido());
        await service.CriarAditivoAsync(contrato.Id, CriarAditivoDtoValido());

        var lista = await service.ListarAditivosAsync(contrato.Id);

        Assert.Equal(2, lista.Count);
        Assert.Equal(1, lista[0].Numero);
        Assert.Equal(2, lista[1].Numero);
    }
}

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

// Perfil Administrativo só enxerga/mexe no que foi criado pela administração (OC e despesa avulsa com
// CriadaPelaAdministracao = true, e as obrigações delas). Administrador (sem escopo restrito) vê tudo.
public class EscopoAdministracaoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    private sealed class EscopoFalso : IEscopoAcesso
    {
        public bool RestritoAAdministracao { get; set; }
        public int? UsuarioId { get; set; }
    }

    private sealed class Cenario
    {
        public AppDbContext Context = null!;
        public EscopoFalso Escopo = null!;
        public OrdemCompraService OrdemCompra = null!;
        public DespesaAvulsaService Despesa = null!;
        public ObrigacaoService Obrigacao = null!;
        public SolicitacaoPagamentoService Solicitacao = null!;
        public Fornecedor Fornecedor = null!;
        public Local Local = null!;
        public Usuario Andre = null!;
        // Dados semeados: 1 OC e 1 despesa da administração, 1 OC e 1 despesa de Administrador, 1 OC antiga (sem criador).
        public OrdemCompra OcAdm = null!, OcAdmin = null!, OcAntiga = null!;
        public DespesaAvulsa DespesaAdm = null!, DespesaAdmin = null!;
        public Obrigacao ObrAdm = null!, ObrAdmin = null!, ObrAntiga = null!, ObrDespesaAdm = null!, ObrDespesaAdmin = null!, ObrMedicao = null!;
    }

    private static Cenario Montar(bool restrito)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var context = new AppDbContext(options);
        var tempo = new FakeTimeProvider(Agora);
        var escopo = new EscopoFalso { RestritoAAdministracao = restrito };
        var config = new ConfigurationBuilder().Build();
        var c = new Cenario
        {
            Context = context,
            Escopo = escopo,
            OrdemCompra = new OrdemCompraService(context, tempo, NullLogger<OrdemCompraService>.Instance, config, escopo),
            Despesa = new DespesaAvulsaService(context, tempo, NullLogger<DespesaAvulsaService>.Instance, escopo),
            Obrigacao = new ObrigacaoService(context, tempo, NullLogger<ObrigacaoService>.Instance, escopo),
            Solicitacao = new SolicitacaoPagamentoService(context, tempo, escopo),
        };

        var agora = Agora.UtcDateTime;
        c.Fornecedor = new Fornecedor { Nome = "Fornecedor", Ativo = true, DataCriacao = agora, DataAtualizacao = agora };
        c.Local = new Local { Nome = "Obra", Ativo = true, DataCriacao = agora, DataAtualizacao = agora };
        c.Andre = new Usuario { Nome = "André", Email = "andre@empresa.com", DataInicio = new DateOnly(2026, 1, 1), DataCriacao = agora, DataAtualizacao = agora };
        context.AddRange(c.Fornecedor, c.Local, c.Andre);
        context.SaveChanges();

        OrdemCompra NovaOc(int numero, bool administracao, int? criadoPor) => new()
        {
            Numero = numero, Data = new DateOnly(2026, 9, 1), Solicitante = "x", LocalId = c.Local.Id, FornecedorId = c.Fornecedor.Id,
            CondicaoPagamento = "30 dias", Status = OrdemCompraStatus.Rascunho, CriadaPelaAdministracao = administracao,
            CriadoPorUsuarioId = criadoPor, DataCriacao = agora, DataAtualizacao = agora,
            Itens = [new OrdemCompraItem { Descricao = "Item", Unidade = "un", Quantidade = 1, ValorUnitario = 100, DataCriacao = agora, DataAtualizacao = agora }],
        };
        DespesaAvulsa NovaDespesa(bool administracao, int? criadoPor) => new()
        {
            FornecedorId = c.Fornecedor.Id, Categoria = DespesaAvulsaCategoria.Servicos, Descricao = "Despesa", Valor = 50,
            CriadaPelaAdministracao = administracao, CriadoPorUsuarioId = criadoPor, DataCriacao = agora, DataAtualizacao = agora,
        };
        c.OcAdm = NovaOc(1, true, c.Andre.Id);
        c.OcAdmin = NovaOc(2, false, null);
        c.OcAntiga = NovaOc(3, false, null);
        c.DespesaAdm = NovaDespesa(true, c.Andre.Id);
        c.DespesaAdmin = NovaDespesa(false, null);
        context.AddRange(c.OcAdm, c.OcAdmin, c.OcAntiga, c.DespesaAdm, c.DespesaAdmin);
        context.SaveChanges();

        Obrigacao NovaObrigacao(string tipo, int? ocId = null, int? despesaId = null, int? bmId = null) => new()
        {
            TipoMovimento = tipo, OrdemCompraId = ocId, DespesaAvulsaId = despesaId, MedicaoBmId = bmId, FornecedorId = c.Fornecedor.Id,
            Competencia = new DateOnly(2026, 9, 1), ValorPrevisto = 100, DataCriacao = agora, DataAtualizacao = agora,
        };
        var contrato = new Contrato
        {
            Numero = "C-1", FornecedorId = c.Fornecedor.Id, Objeto = "Obj", DataAssinatura = new DateOnly(2026, 1, 1),
            DataInicioVigencia = new DateOnly(2026, 1, 1), DataFimVigenciaOriginal = new DateOnly(2026, 12, 31), ValorOriginal = 1000,
            Status = ContratoStatus.Ativo, DataCriacao = agora, DataAtualizacao = agora,
        };
        context.Contratos.Add(contrato);
        context.SaveChanges();
        var bm = new MedicaoBm
        {
            ContratoId = contrato.Id, Numero = 1, PeriodoInicio = new DateOnly(2026, 9, 1), PeriodoFim = new DateOnly(2026, 9, 30),
            Status = MedicaoBmStatus.Rascunho, DataCriacao = agora, DataAtualizacao = agora,
        };
        context.MedicaoBms.Add(bm);
        context.SaveChanges();

        c.ObrAdm = NovaObrigacao(ObrigacaoTipoMovimento.OrdemCompra, ocId: c.OcAdm.Id);
        c.ObrAdmin = NovaObrigacao(ObrigacaoTipoMovimento.OrdemCompra, ocId: c.OcAdmin.Id);
        c.ObrAntiga = NovaObrigacao(ObrigacaoTipoMovimento.OrdemCompra, ocId: c.OcAntiga.Id);
        c.ObrDespesaAdm = NovaObrigacao(ObrigacaoTipoMovimento.DespesaAvulsa, despesaId: c.DespesaAdm.Id);
        c.ObrDespesaAdmin = NovaObrigacao(ObrigacaoTipoMovimento.DespesaAvulsa, despesaId: c.DespesaAdmin.Id);
        c.ObrMedicao = NovaObrigacao(ObrigacaoTipoMovimento.Medicao, bmId: bm.Id);
        context.AddRange(c.ObrAdm, c.ObrAdmin, c.ObrAntiga, c.ObrDespesaAdm, c.ObrDespesaAdmin, c.ObrMedicao);
        context.SaveChanges();

        context.OrdemCompraAnexos.Add(new OrdemCompraAnexo { OrdemCompraId = c.OcAdmin.Id, NomeArquivo = "a.pdf", TipoConteudo = "application/pdf", Tamanho = 1, Conteudo = [1], DataUpload = agora });
        context.DespesaAvulsaAnexos.Add(new DespesaAvulsaAnexo { DespesaAvulsaId = c.DespesaAdmin.Id, NomeArquivo = "a.pdf", TipoConteudo = "application/pdf", Tamanho = 1, Conteudo = [1], DataUpload = agora });
        context.SaveChanges();
        return c;
    }

    // ---------- Administrativo: só enxerga o que é da administração ----------

    [Fact]
    public async Task Obrigacoes_AdministrativoListaSoAsDaAdministracao()
    {
        var c = Montar(restrito: true);

        var lista = await c.Obrigacao.GetAllAsync(new ObrigacaoFiltroDto());

        Assert.Equal([c.ObrAdm.Id, c.ObrDespesaAdm.Id], lista.Select(o => o.Id).OrderBy(i => i));
    }

    [Fact]
    public async Task Obrigacoes_AdministradorListaTodas()
    {
        var c = Montar(restrito: false);

        var lista = await c.Obrigacao.GetAllAsync(new ObrigacaoFiltroDto());

        Assert.Equal(6, lista.Count);
    }

    [Fact]
    public async Task Obrigacoes_AdministrativoNaoAbreNemAlteraAsDeFora()
    {
        var c = Montar(restrito: true);

        foreach (var id in new[] { c.ObrAdmin.Id, c.ObrAntiga.Id, c.ObrDespesaAdmin.Id, c.ObrMedicao.Id })
        {
            await Assert.ThrowsAsync<NotFoundException>(() => c.Obrigacao.GetByIdAsync(id));
            await Assert.ThrowsAsync<NotFoundException>(() => c.Obrigacao.UpdateAsync(id, new UpdateObrigacaoDto()));
            await Assert.ThrowsAsync<NotFoundException>(() => c.Obrigacao.MarcarPagaAsync(id));
            await Assert.ThrowsAsync<NotFoundException>(() => c.Obrigacao.CancelarAsync(id));
            await Assert.ThrowsAsync<NotFoundException>(() => c.Obrigacao.ReativarAsync(id));
            await Assert.ThrowsAsync<NotFoundException>(() => c.Obrigacao.AtualizarAndamentoAsync(id, new AtualizarAndamentoObrigacaoDto()));
            await Assert.ThrowsAsync<NotFoundException>(() => c.Obrigacao.AdicionarParcelaAsync(id, new AdicionarParcelaObrigacaoDto { Valor = 10 }));
            await Assert.ThrowsAsync<NotFoundException>(() => c.Obrigacao.MarcarEnviadaFinanceiroAsync(id, new MarcarEnviadaFinanceiroDto()));
            await Assert.ThrowsAsync<NotFoundException>(() => c.Solicitacao.GerarAsync(id, null));
        }
    }

    [Fact]
    public async Task Obrigacoes_AdministrativoMexeNasDaAdministracao()
    {
        var c = Montar(restrito: true);

        var aberta = await c.Obrigacao.GetByIdAsync(c.ObrAdm.Id);
        Assert.Equal("André", aberta.CriadoPorNome);

        var paga = await c.Obrigacao.MarcarPagaAsync(c.ObrAdm.Id);
        Assert.True(paga.Pago);
        var cancelada = await c.Obrigacao.CancelarAsync(c.ObrDespesaAdm.Id);
        Assert.True(cancelada.Cancelada);
    }

    [Fact]
    public async Task OrdemCompra_AdministrativoListaSoAsDaAdministracaoEBloqueiaAsDeFora()
    {
        var c = Montar(restrito: true);

        var lista = await c.OrdemCompra.GetAllAsync(new OrdemCompraFiltroDto());
        Assert.Equal([c.OcAdm.Id], lista.Select(o => o.Id));
        Assert.Equal("André", lista[0].CriadoPorNome);

        foreach (var id in new[] { c.OcAdmin.Id, c.OcAntiga.Id })
        {
            await Assert.ThrowsAsync<NotFoundException>(() => c.OrdemCompra.GetByIdAsync(id));
            await Assert.ThrowsAsync<NotFoundException>(() => c.OrdemCompra.EmitirAsync(id));
            await Assert.ThrowsAsync<NotFoundException>(() => c.OrdemCompra.ReabrirAsync(id));
            await Assert.ThrowsAsync<NotFoundException>(() => c.OrdemCompra.MarcarAssinadaAsync(id));
            await Assert.ThrowsAsync<NotFoundException>(() => c.OrdemCompra.CancelarAsync(id));
            await Assert.ThrowsAsync<NotFoundException>(() => c.OrdemCompra.GerarPdfAsync(id));
            await Assert.ThrowsAsync<NotFoundException>(() => c.OrdemCompra.ListarAnexosAsync(id));
        }

        // O anexo existe, mas a OC é de Administrador: nem baixar nem excluir.
        var anexoId = (await c.Context.OrdemCompraAnexos.SingleAsync()).Id;
        await Assert.ThrowsAsync<NotFoundException>(() => c.OrdemCompra.ObterAnexoAsync(c.OcAdmin.Id, anexoId));
        await Assert.ThrowsAsync<NotFoundException>(() => c.OrdemCompra.ExcluirAnexoAsync(c.OcAdmin.Id, anexoId));
    }

    [Fact]
    public async Task Despesa_AdministrativoListaSoAsDaAdministracaoEBloqueiaAsDeFora()
    {
        var c = Montar(restrito: true);

        var lista = await c.Despesa.GetAllAsync(new DespesaAvulsaFiltroDto());
        Assert.Equal([c.DespesaAdm.Id], lista.Select(d => d.Id));

        await Assert.ThrowsAsync<NotFoundException>(() => c.Despesa.GetByIdAsync(c.DespesaAdmin.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => c.Despesa.DeleteAsync(c.DespesaAdmin.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => c.Despesa.ListarAnexosAsync(c.DespesaAdmin.Id));
        var anexoId = (await c.Context.DespesaAvulsaAnexos.SingleAsync()).Id;
        await Assert.ThrowsAsync<NotFoundException>(() => c.Despesa.ObterAnexoAsync(c.DespesaAdmin.Id, anexoId));
        await Assert.ThrowsAsync<NotFoundException>(() => c.Despesa.ExcluirAnexoAsync(c.DespesaAdmin.Id, anexoId));
    }

    [Fact]
    public async Task OrdemCompraEDespesa_AdministradorVeTudo()
    {
        var c = Montar(restrito: false);

        Assert.Equal(3, (await c.OrdemCompra.GetAllAsync(new OrdemCompraFiltroDto())).Count);
        Assert.Equal(2, (await c.Despesa.GetAllAsync(new DespesaAvulsaFiltroDto())).Count);
        Assert.NotNull(await c.OrdemCompra.GetByIdAsync(c.OcAdmin.Id));
    }

    // ---------- Criação grava o criador e a marca ----------

    [Fact]
    public async Task CriarOrdemCompra_AdministrativoGravaCriadorEMarcaDaAdministracao()
    {
        var c = Montar(restrito: true);
        c.Escopo.UsuarioId = c.Andre.Id;

        var criada = await c.OrdemCompra.CreateAsync(NovaOc(c));

        var salva = await c.Context.OrdensCompra.SingleAsync(o => o.Id == criada.Id);
        Assert.True(salva.CriadaPelaAdministracao);
        Assert.Equal(c.Andre.Id, salva.CriadoPorUsuarioId);
        // A obrigação nascida da OC fica visível para o Administrativo.
        Assert.Contains(await c.Obrigacao.GetAllAsync(new ObrigacaoFiltroDto()), o => o.OrdemCompraId == criada.Id);
    }

    [Fact]
    public async Task CriarOrdemCompra_AdministradorNaoMarcaComoDaAdministracao()
    {
        var c = Montar(restrito: false);
        c.Escopo.UsuarioId = c.Andre.Id;

        var criada = await c.OrdemCompra.CreateAsync(NovaOc(c));

        var salva = await c.Context.OrdensCompra.SingleAsync(o => o.Id == criada.Id);
        Assert.False(salva.CriadaPelaAdministracao);
        Assert.Equal(c.Andre.Id, salva.CriadoPorUsuarioId);
    }

    [Fact]
    public async Task CriarDespesa_AdministrativoGravaCriadorEMarcaDaAdministracao()
    {
        var c = Montar(restrito: true);
        c.Escopo.UsuarioId = c.Andre.Id;
        var setor = new Setor { Nome = "Setor", DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        var ua = new UnidadeOrcamentaria { Setor = setor, Codigo = "UA-01", Descricao = "UA", DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        c.Context.UnidadesOrcamentarias.Add(ua);
        await c.Context.SaveChangesAsync();

        var criada = await c.Despesa.CreateAsync(new CreateDespesaAvulsaDto
        {
            FornecedorId = c.Fornecedor.Id, Categoria = DespesaAvulsaCategoria.Servicos, Descricao = "Nova", Valor = 80,
            RateioUa = [new ItemRateioUaValorInputDto { UnidadeOrcamentariaId = ua.Id, Valor = 80 }],
        });

        var salva = await c.Context.DespesasAvulsas.SingleAsync(d => d.Id == criada.Id);
        Assert.True(salva.CriadaPelaAdministracao);
        Assert.Equal(c.Andre.Id, salva.CriadoPorUsuarioId);
        Assert.Contains(await c.Obrigacao.GetAllAsync(new ObrigacaoFiltroDto()), o => o.DespesaAvulsaId == criada.Id);
    }

    private static CreateOrdemCompraDto NovaOc(Cenario c) => new()
    {
        Data = new DateOnly(2026, 9, 10), Solicitante = "x", LocalId = c.Local.Id, FornecedorId = c.Fornecedor.Id, CondicaoPagamento = "30 dias",
        Itens = [new CreateOrdemCompraItemDto { Descricao = "Item", Unidade = "un", Quantidade = 1, ValorUnitario = 10 }],
    };
}

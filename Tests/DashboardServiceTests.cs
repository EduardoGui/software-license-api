using Microsoft.EntityFrameworkCore;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class DashboardServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(Agora.Date);

    private static (DashboardService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var relatorioMensalLocacaoService = new RelatorioMensalLocacaoService(context);
        var service = new DashboardService(context, new FakeTimeProvider(Agora), relatorioMensalLocacaoService);
        return (service, context);
    }

    private static TipoEquipamento CriarTipoEquipamento(AppDbContext context, string nome = "Notebook")
    {
        var tipo = new TipoEquipamento { Nome = nome, Ativo = true, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        context.TiposEquipamento.Add(tipo);
        context.SaveChanges();
        return tipo;
    }

    private static Equipamento CriarEquipamento(
        AppDbContext context,
        TipoEquipamento tipo,
        string origem = "Comprado",
        string status = "Disponivel",
        decimal? valorMensal = null,
        DateOnly? dataInicioContrato = null,
        DateOnly? dataFimContrato = null)
    {
        var equipamento = new Equipamento
        {
            TipoEquipamentoId = tipo.Id,
            Origem = origem,
            Status = status,
            ValorMensal = valorMensal,
            DataInicioContrato = dataInicioContrato,
            DataFimContrato = dataFimContrato,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Equipamentos.Add(equipamento);
        context.SaveChanges();
        return equipamento;
    }

    private static Usuario CriarUsuario(AppDbContext context, string nome, DateOnly dataInicio, DateOnly? dataFim = null)
    {
        var usuario = new Usuario
        {
            Nome = nome,
            Email = $"{nome.ToLowerInvariant()}@empresa.com",
            DataInicio = dataInicio,
            DataFim = dataFim,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Usuarios.Add(usuario);
        context.SaveChanges();
        return usuario;
    }

    private static Licenca CriarLicenca(
        AppDbContext context,
        string nome,
        int quantidadeTotal,
        DateOnly dataTerminoPrevisto,
        int diasAntecedenciaAviso = 30,
        bool ativa = true)
    {
        var licenca = new Licenca
        {
            Nome = nome,
            QuantidadeTotal = quantidadeTotal,
            DataInicio = Hoje.AddYears(-1),
            DataTerminoPrevisto = dataTerminoPrevisto,
            DiasAntecedenciaAviso = diasAntecedenciaAviso,
            Ativa = ativa,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Licencas.Add(licenca);
        context.SaveChanges();
        return licenca;
    }

    [Fact]
    public async Task ObterAsync_DeveContarUsuariosAtivosCorretamente()
    {
        var (service, context) = CriarService();
        CriarUsuario(context, "Ana", Hoje.AddDays(-10));
        CriarUsuario(context, "Joao", Hoje.AddDays(10));
        CriarUsuario(context, "Maria", Hoje.AddDays(-30), Hoje.AddDays(-1));

        var dashboard = await service.ObterAsync();

        Assert.Equal(1, dashboard.UsuariosAtivos);
    }

    [Fact]
    public async Task ObterAsync_DeveCalcularQuantidadesDeLicencasCorretamente()
    {
        var (service, context) = CriarService();
        var licenca = CriarLicenca(context, "Microsoft 365", quantidadeTotal: 20, dataTerminoPrevisto: Hoje.AddYears(1));
        var usuario = CriarUsuario(context, "Ana", Hoje.AddDays(-10));

        context.UsuarioLicencas.Add(new UsuarioLicenca
        {
            UsuarioId = usuario.Id,
            LicencaId = licenca.Id,
            DataInicio = Hoje.AddDays(-5),
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        });
        await context.SaveChangesAsync();

        var dashboard = await service.ObterAsync();

        Assert.Equal(20, dashboard.LicencasAdquiridas);
        Assert.Equal(1, dashboard.LicencasEmUso);
        Assert.Equal(19, dashboard.LicencasDisponiveis);
    }

    [Fact]
    public async Task ObterAsync_DeveAgruparLicencasEmUsoPorNome()
    {
        var (service, context) = CriarService();
        var m365 = CriarLicenca(context, "Microsoft 365", quantidadeTotal: 20, dataTerminoPrevisto: Hoje.AddYears(1));
        // 2º lote da mesma licença, cadastrado como registro separado — deve somar na mesma linha.
        var m365Lote2 = CriarLicenca(context, "Microsoft 365", quantidadeTotal: 10, dataTerminoPrevisto: Hoje.AddYears(1));
        var autocad = CriarLicenca(context, "AutoCAD", quantidadeTotal: 5, dataTerminoPrevisto: Hoje.AddYears(1));
        var ana = CriarUsuario(context, "Ana", Hoje.AddDays(-10));
        var joao = CriarUsuario(context, "Joao", Hoje.AddDays(-10));
        var maria = CriarUsuario(context, "Maria", Hoje.AddDays(-10));

        context.UsuarioLicencas.Add(new UsuarioLicenca { UsuarioId = ana.Id, LicencaId = m365.Id, DataInicio = Hoje.AddDays(-5), DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime });
        context.UsuarioLicencas.Add(new UsuarioLicenca { UsuarioId = joao.Id, LicencaId = m365.Id, DataInicio = Hoje.AddDays(-5), DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime });
        context.UsuarioLicencas.Add(new UsuarioLicenca { UsuarioId = maria.Id, LicencaId = m365Lote2.Id, DataInicio = Hoje.AddDays(-5), DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime });
        // AutoCAD (autocad) nunca foi alocado — não deve aparecer na lista de "em uso".
        await context.SaveChangesAsync();

        var dashboard = await service.ObterAsync();

        var item = Assert.Single(dashboard.LicencasEmUsoPorNome);
        Assert.Equal("Microsoft 365", item.Nome);
        Assert.Equal(3, item.Quantidade);
    }

    [Fact]
    public async Task ObterAsync_DeveAgruparLicencasDisponiveisPorNomeExcluindoZeradasEInativas()
    {
        var (service, context) = CriarService();
        var m365 = CriarLicenca(context, "Microsoft 365", quantidadeTotal: 20, dataTerminoPrevisto: Hoje.AddYears(1));
        var autocad = CriarLicenca(context, "AutoCAD", quantidadeTotal: 1, dataTerminoPrevisto: Hoje.AddYears(1));
        CriarLicenca(context, "Adobe (inativa)", quantidadeTotal: 10, dataTerminoPrevisto: Hoje.AddYears(1), ativa: false);
        var ana = CriarUsuario(context, "Ana", Hoje.AddDays(-10));

        // AutoCAD: única licença, já alocada -> 0 disponíveis, não deve aparecer na lista.
        context.UsuarioLicencas.Add(new UsuarioLicenca { UsuarioId = ana.Id, LicencaId = autocad.Id, DataInicio = Hoje.AddDays(-5), DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime });
        await context.SaveChangesAsync();

        var dashboard = await service.ObterAsync();

        var item = Assert.Single(dashboard.LicencasDisponiveisPorNome);
        Assert.Equal("Microsoft 365", item.Nome);
        Assert.Equal(20, item.Quantidade);
    }

    [Fact]
    public async Task ObterAsync_DeveListarApenasLicencasAtivasDentroDoPrazoDeAviso()
    {
        var (service, context) = CriarService();
        CriarLicenca(context, "Vence em breve", quantidadeTotal: 5, dataTerminoPrevisto: Hoje.AddDays(10), diasAntecedenciaAviso: 30);
        CriarLicenca(context, "Vence longe", quantidadeTotal: 5, dataTerminoPrevisto: Hoje.AddDays(200), diasAntecedenciaAviso: 30);
        CriarLicenca(context, "Inativa vencendo", quantidadeTotal: 5, dataTerminoPrevisto: Hoje.AddDays(5), diasAntecedenciaAviso: 30, ativa: false);

        var dashboard = await service.ObterAsync();

        Assert.Single(dashboard.ProximosVencimentos);
        Assert.Equal("Vence em breve", dashboard.ProximosVencimentos[0].Nome);
        Assert.Equal(10, dashboard.ProximosVencimentos[0].DiasParaVencer);
    }

    [Fact]
    public async Task ObterAsync_DeveIncluirLicencaJaVencidaComDiasNegativos()
    {
        var (service, context) = CriarService();
        CriarLicenca(context, "Vencida", quantidadeTotal: 5, dataTerminoPrevisto: Hoje.AddDays(-3), diasAntecedenciaAviso: 30);

        var dashboard = await service.ObterAsync();

        Assert.Single(dashboard.ProximosVencimentos);
        Assert.Equal(-3, dashboard.ProximosVencimentos[0].DiasParaVencer);
    }

    [Fact]
    public async Task ObterAsync_DeveContarEquipamentosLocadosPorStatusEAgruparPorTipo()
    {
        var (service, context) = CriarService();
        var notebook = CriarTipoEquipamento(context, "Notebook");
        var monitor = CriarTipoEquipamento(context, "Monitor");
        var alocado = CriarEquipamento(context, notebook, origem: "Locado", status: "Disponivel");
        CriarEquipamento(context, notebook, origem: "Locado", status: "Disponivel");
        CriarEquipamento(context, monitor, origem: "Locado", status: "Manutencao");
        CriarEquipamento(context, monitor, origem: "Locado", status: "Baixado");

        var usuario = CriarUsuario(context, "Ana", Hoje.AddDays(-10));
        context.EquipamentoAlocacoes.Add(new EquipamentoAlocacao
        {
            EquipamentoId = alocado.Id,
            UsuarioId = usuario.Id,
            DataInicio = Hoje,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        });
        await context.SaveChangesAsync();

        var dashboard = await service.ObterAsync();

        var emUso = Assert.Single(dashboard.EquipamentosEmUsoPorTipo);
        Assert.Equal("Notebook", emUso.TipoEquipamentoNome);
        Assert.Equal(1, emUso.Quantidade);

        var disponivel = Assert.Single(dashboard.EquipamentosDisponiveisPorTipo);
        Assert.Equal("Notebook", disponivel.TipoEquipamentoNome);
        Assert.Equal(1, disponivel.Quantidade);
    }

    [Fact]
    public async Task ObterAsync_NaoDeveContarEquipamentoCompradoNasContagensDeLocados()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipoEquipamento(context);
        var comprado = CriarEquipamento(context, tipo, origem: "Comprado", status: "Disponivel");
        var usuario = CriarUsuario(context, "Ana", Hoje.AddDays(-10));
        context.EquipamentoAlocacoes.Add(new EquipamentoAlocacao
        {
            EquipamentoId = comprado.Id,
            UsuarioId = usuario.Id,
            DataInicio = Hoje,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        });
        await context.SaveChangesAsync();

        var dashboard = await service.ObterAsync();

        Assert.Empty(dashboard.EquipamentosEmUsoPorTipo);
        Assert.Empty(dashboard.EquipamentosDisponiveisPorTipo);
        Assert.Empty(dashboard.EquipamentosLocadosAtivosPorTipo);
    }

    [Fact]
    public async Task ObterAsync_DeveContarLocadosAtivosExcluindoBaixadosEAgruparPorTipo()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipoEquipamento(context, "Notebook");
        CriarEquipamento(context, tipo, origem: "Locado", status: "Disponivel");
        CriarEquipamento(context, tipo, origem: "Locado", status: "Baixado");
        CriarEquipamento(context, tipo, origem: "Comprado", status: "Disponivel");

        var dashboard = await service.ObterAsync();

        var item = Assert.Single(dashboard.EquipamentosLocadosAtivosPorTipo);
        Assert.Equal("Notebook", item.TipoEquipamentoNome);
        Assert.Equal(1, item.Quantidade);
    }

    [Fact]
    public async Task ObterAsync_DeveCalcularCustoMensalLocacaoAtualComRateio()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipoEquipamento(context);
        // Agosto/2026 tem 31 dias; iniciando 16/08 -> 16 dias ativos.
        CriarEquipamento(context, tipo, origem: "Locado", valorMensal: 310m, dataInicioContrato: new DateOnly(2026, 8, 16));

        var dashboard = await service.ObterAsync();

        Assert.Equal(160m, dashboard.CustoMensalLocacaoAtual);
    }

    private static Fornecedor CriarFornecedor(AppDbContext context, string nome = "Fornecedor Teste")
    {
        var fornecedor = new Fornecedor { Nome = nome, Ativo = true, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        context.Fornecedores.Add(fornecedor);
        context.SaveChanges();
        return fornecedor;
    }

    private static Contrato CriarContratoComMedicao(
        AppDbContext context,
        Fornecedor fornecedor,
        string numero,
        int? diaFimPeriodo,
        int? diasAntecedenciaAlerta,
        bool exigeBm = true,
        string status = "Ativo")
    {
        var contrato = new Contrato
        {
            Numero = numero,
            FornecedorId = fornecedor.Id,
            Objeto = "Teste",
            DataAssinatura = Hoje.AddYears(-1),
            DataInicioVigencia = Hoje.AddYears(-1),
            DataFimVigenciaOriginal = Hoje.AddYears(1),
            ValorOriginal = 1000m,
            Status = status,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Contratos.Add(contrato);
        context.SaveChanges();

        context.ContratoMedicaoConfigs.Add(new ContratoMedicaoConfig
        {
            ContratoId = contrato.Id,
            TipoMedicao = "QuantidadeXPrecoUnitario",
            DiaInicioPeriodo = 1,
            DiaFimPeriodo = diaFimPeriodo,
            ExigeBm = exigeBm,
            DiasAntecedenciaAlerta = diasAntecedenciaAlerta,
        });
        context.SaveChanges();

        return contrato;
    }

    [Fact]
    public async Task ObterAsync_DeveAlertarMedicaoQuandoPeriodoAtualEstaDentroDoPrazo()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        // Hoje = 12/08/2026. Período fecha dia 15 (ainda este mês) — faltam 3 dias.
        CriarContratoComMedicao(context, fornecedor, "CT-001", diaFimPeriodo: 15, diasAntecedenciaAlerta: 5);

        var dashboard = await service.ObterAsync();

        var alerta = Assert.Single(dashboard.AlertasMedicao);
        Assert.Equal("CT-001", alerta.ContratoNumero);
        Assert.Equal(new DateOnly(2026, 8, 15), alerta.PeriodoFim);
        Assert.Equal(3, alerta.DiasParaVencer);
    }

    [Fact]
    public async Task ObterAsync_NaoDeveAlertarQuandoPeriodoAindaEstaLonge()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        CriarContratoComMedicao(context, fornecedor, "CT-002", diaFimPeriodo: 28, diasAntecedenciaAlerta: 5);

        var dashboard = await service.ObterAsync();

        Assert.Empty(dashboard.AlertasMedicao);
    }

    [Fact]
    public async Task ObterAsync_DeveRolarParaOMesSeguinteQuandoPeriodoAtualJaPassou()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        // Dia de fechamento (5) já passou este mês (hoje é 12) — o período corrente é o de setembro.
        CriarContratoComMedicao(context, fornecedor, "CT-003", diaFimPeriodo: 5, diasAntecedenciaAlerta: 30);

        var dashboard = await service.ObterAsync();

        var alerta = Assert.Single(dashboard.AlertasMedicao);
        Assert.Equal(new DateOnly(2026, 9, 5), alerta.PeriodoFim);
    }

    [Fact]
    public async Task ObterAsync_NaoDeveAlertarQuandoJaExisteBmParaOPeriodo()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        var contrato = CriarContratoComMedicao(context, fornecedor, "CT-004", diaFimPeriodo: 15, diasAntecedenciaAlerta: 5);
        context.MedicaoBms.Add(new MedicaoBm
        {
            ContratoId = contrato.Id,
            Numero = 1,
            PeriodoInicio = new DateOnly(2026, 8, 1),
            PeriodoFim = new DateOnly(2026, 8, 15),
            Status = MedicaoBmStatus.Rascunho,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        });
        await context.SaveChangesAsync();

        var dashboard = await service.ObterAsync();

        Assert.Empty(dashboard.AlertasMedicao);
    }

    [Fact]
    public async Task ObterAsync_NaoDeveAlertarContratoQueNaoExigeBm()
    {
        var (service, context) = CriarService();
        var fornecedor = CriarFornecedor(context);
        CriarContratoComMedicao(context, fornecedor, "CT-005", diaFimPeriodo: 15, diasAntecedenciaAlerta: 5, exigeBm: false);

        var dashboard = await service.ObterAsync();

        Assert.Empty(dashboard.AlertasMedicao);
    }

    [Fact]
    public async Task ObterAsync_DeveListarContratosVencendoDentroDoPrazo()
    {
        var (service, context) = CriarService();
        var tipo = CriarTipoEquipamento(context);
        CriarEquipamento(
            context, tipo, origem: "Locado", valorMensal: 100m,
            dataInicioContrato: Hoje.AddYears(-1), dataFimContrato: Hoje.AddDays(10));
        CriarEquipamento(
            context, tipo, origem: "Locado", valorMensal: 100m,
            dataInicioContrato: Hoje.AddYears(-1), dataFimContrato: Hoje.AddDays(200));

        var dashboard = await service.ObterAsync();

        Assert.Single(dashboard.ProximosVencimentosContratos);
        Assert.Equal(10, dashboard.ProximosVencimentosContratos[0].DiasParaVencer);
    }
}

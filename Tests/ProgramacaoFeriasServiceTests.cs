using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class ProgramacaoFeriasServiceTests
{
    // "Hoje" fixado depois do fim do aquisitivo (2026-12-31) mas dentro do concessivo
    // (2027-01-01 a 2027-12-31) - período com direito adquirido de verdade, pronto pra programar.
    private static readonly DateTimeOffset Agora = new(2027, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private static (ProgramacaoFeriasService Service, PeriodoFeriasService PeriodoService, AppDbContext Context) CriarServicos()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var timeProvider = new FakeTimeProvider(Agora);
        var auditoriaService = new AuditoriaService(context, timeProvider);
        var periodoService = new PeriodoFeriasService(context, timeProvider, auditoriaService, NullLogger<PeriodoFeriasService>.Instance);
        var service = new ProgramacaoFeriasService(context, timeProvider, auditoriaService, periodoService, NullLogger<ProgramacaoFeriasService>.Instance);
        return (service, periodoService, context);
    }

    private static Usuario CriarUsuarioPj(AppDbContext context, string nome = "Colaborador PJ") => CriarUsuarioComTipo(context, UsuarioTipo.Pj, nome);

    private static Usuario CriarUsuarioComTipo(AppDbContext context, string tipo, string nome)
    {
        var usuario = new Usuario
        {
            Nome = nome,
            Email = $"{Guid.NewGuid():N}@empresa.com",
            DataInicio = new DateOnly(2026, 1, 1),
            Tipo = tipo,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.Usuarios.Add(usuario);
        context.SaveChanges();
        return usuario;
    }

    private static void CriarPoliticaPj(AppDbContext context, int diasMinimosAntesFeriadoOuFimDeSemana = 2)
    {
        context.PoliticasFerias.Add(new PoliticaFerias
        {
            TipoVinculo = UsuarioTipo.Pj,
            DiasDireitoPorAno = 30,
            MaxFracionamentos = 3,
            DiasMinimoUltimoFracionamento = 14,
            DiasMinimoDemaisFracionamentos = 5,
            DiasAntecedenciaRemarcacao = 45,
            DiasAntecedenciaMarcacaoCompulsoria = 30,
            PermiteAbonoPecuniario = true,
            MaxDiasAbono = 10,
            DiasMinimosAntesFeriadoOuFimDeSemana = diasMinimosAntesFeriadoOuFimDeSemana,
            Ativa = true,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        });
        context.SaveChanges();
    }

    /// Período já com aquisitivo fechado (2026-01-01 a 2026-12-31) e saldo de 30 dias materializado,
    /// concessivo em curso (2027-01-01 a 2027-12-31) - pronto para programar férias de verdade.
    private static async Task<PeriodoFeriasDto> CriarPeriodoComSaldoAsync(PeriodoFeriasService periodoService, int usuarioId)
    {
        return await periodoService.GerarProximoPeriodoAsync(usuarioId, usuarioResponsavelId: null);
    }

    /// Simula um Recesso Corporativo já confirmado pra este colaborador (sem passar pelo
    /// RecessoCorporativoService - só o suficiente pra testar como ProgramacaoFeriasService reage
    /// a um bloco já consumido por recesso).
    private static void CriarRecessoConfirmado(AppDbContext context, int usuarioId, int periodoFeriasId, int diasAbatidos)
    {
        var recesso = new RecessoCorporativo
        {
            Nome = "Recesso de Fim de Ano (teste)",
            DataInicio = new DateOnly(2026, 12, 21),
            DataFim = new DateOnly(2027, 1, 3),
            DiasCorridos = 14,
            DiasADescontar = diasAbatidos,
            Status = RecessoCorporativoStatus.Confirmado,
            DataCriacao = Agora.UtcDateTime,
            DataAtualizacao = Agora.UtcDateTime,
        };
        context.RecessosCorporativos.Add(recesso);
        context.SaveChanges();

        var colaborador = new RecessoColaborador
        {
            RecessoCorporativoId = recesso.Id,
            UsuarioId = usuarioId,
            PeriodoFeriasId = periodoFeriasId,
            SaldoAnterior = 30,
            DiasAbatidos = diasAbatidos,
            SaldoPosterior = 30 - diasAbatidos,
            Situacao = RecessoColaboradorSituacao.Normal,
            DataCriacao = Agora.UtcDateTime,
        };
        context.RecessosColaborador.Add(colaborador);
        context.SaveChanges();

        context.MovimentacoesSaldoFerias.Add(new MovimentacaoSaldoFerias
        {
            PeriodoFeriasId = periodoFeriasId,
            Tipo = MovimentacaoSaldoFeriasTipo.Recesso,
            Quantidade = -diasAbatidos,
            RecessoColaboradorId = colaborador.Id,
            Data = recesso.DataInicio,
            UsuarioResponsavelId = null,
            Observacao = null,
            DataCriacao = Agora.UtcDateTime,
        });
        context.SaveChanges();
    }

    private static CreateProgramacaoFeriasDto CriarDtoValido(DateOnly? dataInicio = null, int quantidadeDias = 10) => new()
    {
        DataInicio = dataInicio ?? new DateOnly(2027, 1, 11), // segunda-feira, sem feriado/fim de semana perto
        QuantidadeDias = quantidadeDias,
    };

    [Fact]
    public async Task CreateAsync_DeveCriarComoRascunho()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);

        var programacao = await service.CreateAsync(periodo.Id, CriarDtoValido(), usuarioResponsavelId: null);

        Assert.Equal(ProgramacaoFeriasStatus.Rascunho, programacao.Status);
        Assert.Equal(1, programacao.Sequencia);
        Assert.Equal(new DateOnly(2027, 1, 20), programacao.DataFim);
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarQuandoUltrapassaFimConcessivo()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);

        var dto = CriarDtoValido(dataInicio: new DateOnly(2027, 12, 28), quantidadeDias: 10);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(periodo.Id, dto, null));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarInicioProximoDeFimDeSemana()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);

        // 07/01/2027 é quinta-feira - 2 dias depois cai no sábado.
        var dto = CriarDtoValido(dataInicio: new DateOnly(2027, 1, 7), quantidadeDias: 10);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(periodo.Id, dto, null));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarInicioProximoDeFeriado()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        context.Feriados.Add(new Feriado
        {
            Data = new DateOnly(2027, 1, 13), Descricao = "Feriado de teste", Abrangencia = FeriadoAbrangencia.Nacional, Ativo = true,
            DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime,
        });
        context.SaveChanges();
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);

        // 11/01/2027 é segunda-feira - feriado cai 2 dias depois (13/01).
        var dto = CriarDtoValido(dataInicio: new DateOnly(2027, 1, 11), quantidadeDias: 10);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(periodo.Id, dto, null));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarSobreposicaoComOutraProgramacaoAtiva()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);
        await service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 1, 11), 10), null); // 11 a 20/01

        var dtoSobreposto = CriarDtoValido(new DateOnly(2027, 1, 18), 5); // 18 a 22/01, sobrepõe

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(periodo.Id, dtoSobreposto, null));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarSaldoInsuficiente()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);

        var dto = CriarDtoValido(quantidadeDias: 30);
        await periodoService.RegistrarAjusteManualAsync(periodo.Id, new CreateAjusteManualSaldoFeriasDto { Quantidade = -5, Observacao = "Reduz saldo para teste" }, null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(periodo.Id, dto, null));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarMaximoDeFracionamentos()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);

        await service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 1, 11), 14), null);
        await service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 2, 8), 5), null);
        await service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 3, 8), 5), null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 4, 12), 5), null));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarSeNenhumFracionamentoTiverOMinimoDoUltimo()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);

        await service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 1, 11), 5), null);

        // Segundo fracionamento também com 5 dias - nenhum dos dois chega aos 14 mínimos.
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 2, 8), 5), null));
    }

    [Fact]
    public async Task CreateAsync_DeveConsiderarRecessoConfirmadoComoOFracionamentoMaior()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);
        // Recesso já consumiu 14 dias de uma vez - satisfaz por si só a exigência do "maior".
        CriarRecessoConfirmado(context, usuario.Id, periodo.Id, diasAbatidos: 14);

        // Sem o recesso contar, nenhum desses dois fracionamentos (5 dias cada) atingiria o mínimo
        // de 14 - com o recesso contando, ambos são aceitos (achado real do usuário em produção).
        var primeira = await service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 1, 11), 5), null);
        var segunda = await service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 2, 8), 5), null);

        Assert.Equal(ProgramacaoFeriasStatus.Rascunho, primeira.Status);
        Assert.Equal(ProgramacaoFeriasStatus.Rascunho, segunda.Status);
    }

    [Fact]
    public async Task CreateAsync_DeveContarRecessoNoMaximoDeFracionamentos()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);
        CriarRecessoConfirmado(context, usuario.Id, periodo.Id, diasAbatidos: 14);

        // MaxFracionamentos=3: recesso (1) + 2 programações já preenchem os 3 - a 3ª programação
        // deveria ser rejeitada por máximo de fracionamentos, contando o recesso como um deles.
        await service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 1, 11), 5), null);
        await service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 2, 8), 5), null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 3, 8), 5), null));
    }

    [Fact]
    public async Task UpdateAsync_DevePermitirEditarEnquantoRascunho()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);
        var programacao = await service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 1, 11), 10), null);

        // 08/02/2027 é segunda-feira - troca a data e a quantidade de dias.
        var editada = await service.UpdateAsync(programacao.Id, CriarDtoValido(new DateOnly(2027, 2, 8), 7), null);

        Assert.Equal(new DateOnly(2027, 2, 8), editada.DataInicio);
        Assert.Equal(new DateOnly(2027, 2, 14), editada.DataFim);
        Assert.Equal(7, editada.QuantidadeDias);
        Assert.Equal(ProgramacaoFeriasStatus.Rascunho, editada.Status);
    }

    [Fact]
    public async Task UpdateAsync_NaoDeveConsiderarSobreposicaoComElaMesma()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);
        var programacao = await service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 1, 11), 10), null);

        // Editar mantendo as mesmas datas não deveria disparar "sobrepõe com outra programação" -
        // a própria programação sendo editada precisa ficar de fora dessa checagem.
        var editada = await service.UpdateAsync(programacao.Id, CriarDtoValido(new DateOnly(2027, 1, 11), 10), null);

        Assert.Equal(new DateOnly(2027, 1, 11), editada.DataInicio);
    }

    [Fact]
    public async Task UpdateAsync_DeveRejeitarEdicaoQuandoSolicitada()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);
        var programacao = await service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 1, 11), 10), null);
        await service.SolicitarAsync(programacao.Id, null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateAsync(programacao.Id, CriarDtoValido(new DateOnly(2027, 2, 8), 7), null));
    }

    [Fact]
    public async Task UpdateAsync_DevePermitirEditarAprovadaComEstornoENovoDebito()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);
        // Início bem no futuro (dezembro) pra caber na janela de 45 dias de antecedência.
        var programacao = await service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 12, 6), 10), null);
        await service.SolicitarAsync(programacao.Id, null);
        await service.AprovarAsync(programacao.Id, null);

        // 2027-12-13 é segunda-feira - remarca pra uma semana depois, com menos dias.
        var editada = await service.UpdateAsync(programacao.Id, CriarDtoValido(new DateOnly(2027, 12, 13), 7), null);

        Assert.Equal(ProgramacaoFeriasStatus.Aprovada, editada.Status);
        Assert.Equal(new DateOnly(2027, 12, 13), editada.DataInicio);
        Assert.Equal(7, editada.QuantidadeDias);

        var periodoAtualizado = await periodoService.GetByIdAsync(periodo.Id);
        Assert.Equal(23, periodoAtualizado.SaldoDisponivel); // 30 - 7

        var movimentacoes = await periodoService.GetMovimentacoesAsync(periodo.Id);
        Assert.Contains(movimentacoes, m => m.Quantidade == 10 && m.Observacao != null && m.Observacao.Contains("Estorno"));
        Assert.Contains(movimentacoes, m => m.Quantidade == -7 && m.Observacao != null && m.Observacao.Contains("Novo débito"));
        // Nenhuma das movimentações da edição fica marcada como anulada - a programação continua Aprovada.
        Assert.All(movimentacoes, m => Assert.False(m.Anulada));
    }

    [Fact]
    public async Task UpdateAsync_DeveRejeitarEdicaoDeAprovadaForaDaJanelaDeAntecedencia()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);
        // Início logo depois de "hoje" (15/06/2027) - bem dentro dos 45 dias de antecedência mínima.
        var programacao = await service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 6, 21), 5), null);
        await service.SolicitarAsync(programacao.Id, null);
        await service.AprovarAsync(programacao.Id, null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateAsync(programacao.Id, CriarDtoValido(new DateOnly(2027, 6, 21), 6), null));
    }

    [Fact]
    public async Task UpdateAsync_NaoDeveGerarMovimentacoesQuandoEdicaoDeAprovadaNaoAfetaSaldo()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);
        var programacao = await service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 12, 6), 10), null);
        await service.SolicitarAsync(programacao.Id, null);
        await service.AprovarAsync(programacao.Id, null);
        var movimentacoesAntes = await periodoService.GetMovimentacoesAsync(periodo.Id);

        // Só troca a observação - mesma data, mesma quantidade de dias.
        var dto = CriarDtoValido(new DateOnly(2027, 12, 6), 10);
        dto.Observacao = "Só ajustando a observação";
        await service.UpdateAsync(programacao.Id, dto, null);

        var movimentacoesDepois = await periodoService.GetMovimentacoesAsync(periodo.Id);
        Assert.Equal(movimentacoesAntes.Count, movimentacoesDepois.Count);
    }

    [Fact]
    public async Task UpdateAsync_DevePermitirManterMesmosDiasAoEditarAprovada()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);
        // Programação usa os 30 dias inteiros do saldo - se o estorno não "devolver" o saldo antes
        // de validar, editar mantendo os mesmos 30 dias falharia por "saldo insuficiente".
        var programacao = await service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 11, 1), 30), null);
        await service.SolicitarAsync(programacao.Id, null);
        await service.AprovarAsync(programacao.Id, null);

        var editada = await service.UpdateAsync(programacao.Id, CriarDtoValido(new DateOnly(2027, 11, 1), 30), null);

        Assert.Equal(30, editada.QuantidadeDias);
        var periodoAtualizado = await periodoService.GetByIdAsync(periodo.Id);
        Assert.Equal(0, periodoAtualizado.SaldoDisponivel);
    }

    [Fact]
    public async Task SolicitarAsync_DeveMudarStatusParaSolicitada()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);
        var programacao = await service.CreateAsync(periodo.Id, CriarDtoValido(), null);

        var atualizada = await service.SolicitarAsync(programacao.Id, null);

        Assert.Equal(ProgramacaoFeriasStatus.Solicitada, atualizada.Status);
        Assert.NotNull(atualizada.DataSolicitacao);
    }

    [Fact]
    public async Task AprovarAsync_DeveDebitarSaldoEGerarMovimentacao()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);
        var programacao = await service.CreateAsync(periodo.Id, CriarDtoValido(quantidadeDias: 10), null);
        await service.SolicitarAsync(programacao.Id, null);

        var aprovada = await service.AprovarAsync(programacao.Id, null);

        Assert.Equal(ProgramacaoFeriasStatus.Aprovada, aprovada.Status);
        var periodoAtualizado = await periodoService.GetByIdAsync(periodo.Id);
        Assert.Equal(20, periodoAtualizado.SaldoDisponivel);
    }

    [Fact]
    public async Task AprovarAsync_DeveContarComoConsumidoQuandoDataInicioJaPassou()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);
        // "Hoje" fixado em 15/06/2027 - uma programação iniciada em janeiro já ficou no passado.
        var programacao = await service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 1, 11), 10), null);
        await service.SolicitarAsync(programacao.Id, null);
        await service.AprovarAsync(programacao.Id, null);

        var periodoAtualizado = await periodoService.GetByIdAsync(periodo.Id);

        Assert.Equal(0, periodoAtualizado.Comprometido);
        Assert.Equal(10, periodoAtualizado.Consumido);
    }

    [Fact]
    public async Task AprovarAsync_DeveIncluirDiasDeAbonoNoComprometido()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);
        var dto = CriarDtoValido(new DateOnly(2027, 8, 2), 10);
        dto.AbonoPecuniario = true;
        dto.DiasAbono = 5;
        var programacao = await service.CreateAsync(periodo.Id, dto, null);
        await service.SolicitarAsync(programacao.Id, null);

        await service.AprovarAsync(programacao.Id, null);

        var periodoAtualizado = await periodoService.GetByIdAsync(periodo.Id);
        // Data futura (02/08/2027 vs. "hoje" 15/06/2027) - os 10 dias de férias + 5 de abono devem
        // aparecer juntos no Comprometido (antes desta correção, o abono ficava de fora).
        Assert.Equal(15, periodoAtualizado.Comprometido);
        Assert.Equal(0, periodoAtualizado.Consumido);
        Assert.Equal(15, periodoAtualizado.SaldoDisponivel);
    }

    [Fact]
    public async Task ReprovarAsync_DeveExigirJustificativa()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);
        var programacao = await service.CreateAsync(periodo.Id, CriarDtoValido(), null);
        await service.SolicitarAsync(programacao.Id, null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ReprovarAsync(programacao.Id, new DecisaoProgramacaoFeriasDto { ObservacaoAprovador = "  " }, null));
    }

    [Fact]
    public async Task ReprovarAsync_NaoDeveAfetarSaldo()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);
        var programacao = await service.CreateAsync(periodo.Id, CriarDtoValido(quantidadeDias: 10), null);
        await service.SolicitarAsync(programacao.Id, null);

        await service.ReprovarAsync(programacao.Id, new DecisaoProgramacaoFeriasDto { ObservacaoAprovador = "Período de alta demanda no projeto." }, null);

        var periodoAtualizado = await periodoService.GetByIdAsync(periodo.Id);
        Assert.Equal(30, periodoAtualizado.SaldoDisponivel);
    }

    [Fact]
    public async Task CancelarAsync_DeveDevolverSaldoQuandoJaAprovada()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);
        // Início em dezembro, bem distante de "hoje" (15/06/2027), pra caber na janela de remarcação.
        var programacao = await service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 12, 1), 10), null);
        await service.SolicitarAsync(programacao.Id, null);
        await service.AprovarAsync(programacao.Id, null);

        var cancelada = await service.CancelarAsync(programacao.Id, null);

        Assert.Equal(ProgramacaoFeriasStatus.Cancelada, cancelada.Status);
        var periodoAtualizado = await periodoService.GetByIdAsync(periodo.Id);
        Assert.Equal(30, periodoAtualizado.SaldoDisponivel);
    }

    [Fact]
    public async Task CancelarAsync_DeveMarcarMovimentacaoComoAnuladaNoExtrato()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);
        var programacao = await service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 12, 1), 10), null);
        await service.SolicitarAsync(programacao.Id, null);
        await service.AprovarAsync(programacao.Id, null);

        var antesDeCancelar = await periodoService.GetMovimentacoesAsync(periodo.Id);
        Assert.Contains(antesDeCancelar, m => m.Tipo == MovimentacaoSaldoFeriasTipo.ProgramacaoFerias && !m.Anulada);

        await service.CancelarAsync(programacao.Id, null);

        // A movimentação em si nunca é alterada/apagada (livro-razão), mas o extrato precisa deixar
        // visível que ela não conta mais no saldo (achado do usuário: aparecia como se fosse ativa).
        var depoisDeCancelar = await periodoService.GetMovimentacoesAsync(periodo.Id);
        var movimentacao = Assert.Single(depoisDeCancelar, m => m.Tipo == MovimentacaoSaldoFeriasTipo.ProgramacaoFerias);
        Assert.True(movimentacao.Anulada);
        Assert.Equal(-10, movimentacao.Quantidade);
    }

    [Fact]
    public async Task GetPendentesAprovacaoAsync_DeveListarSoSolicitadas()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);

        var solicitada = await service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 1, 11), 14), null);
        await service.SolicitarAsync(solicitada.Id, null);
        var rascunho = await service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 2, 8), 5), null);

        var pendentes = await service.GetPendentesAprovacaoAsync();

        Assert.Contains(pendentes, p => p.Id == solicitada.Id);
        Assert.DoesNotContain(pendentes, p => p.Id == rascunho.Id);
    }

    [Fact]
    public async Task CancelarAsync_DeveRejeitarForaDoPrazoDeAntecedencia()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario = CriarUsuarioPj(context);
        var periodo = await CriarPeriodoComSaldoAsync(periodoService, usuario.Id);
        // Início logo depois de "hoje" (15/06/2027) - bem dentro dos 45 dias de antecedência mínima.
        var programacao = await service.CreateAsync(periodo.Id, CriarDtoValido(new DateOnly(2027, 6, 21), 5), null);
        await service.SolicitarAsync(programacao.Id, null);
        await service.AprovarAsync(programacao.Id, null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CancelarAsync(programacao.Id, null));
    }

    [Fact]
    public async Task GetByUsuarioAsync_DeveRetornarSoDoUsuarioIndependenteDoStatus()
    {
        var (service, periodoService, context) = CriarServicos();
        CriarPoliticaPj(context);
        var usuario1 = CriarUsuarioPj(context, "Colaborador Um");
        var usuario2 = CriarUsuarioPj(context, "Colaborador Dois");
        var periodo1 = await CriarPeriodoComSaldoAsync(periodoService, usuario1.Id);
        var periodo2 = await CriarPeriodoComSaldoAsync(periodoService, usuario2.Id);

        var rascunho = await service.CreateAsync(periodo1.Id, CriarDtoValido(new DateOnly(2027, 12, 1), 15), null);
        var cancelada = await service.CreateAsync(periodo1.Id, CriarDtoValido(new DateOnly(2027, 8, 4), 5), null);
        await service.CancelarAsync(cancelada.Id, null);
        await service.CreateAsync(periodo2.Id, CriarDtoValido(new DateOnly(2027, 9, 1), 7), null);

        var doUsuario1 = await service.GetByUsuarioAsync(usuario1.Id);

        Assert.Equal(2, doUsuario1.Count);
        Assert.All(doUsuario1, p => Assert.Equal(usuario1.Id, p.UsuarioId));
        Assert.Contains(doUsuario1, p => p.Id == rascunho.Id && p.Status == ProgramacaoFeriasStatus.Rascunho);
        Assert.Contains(doUsuario1, p => p.Id == cancelada.Id && p.Status == ProgramacaoFeriasStatus.Cancelada);
    }
}

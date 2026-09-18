using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class PoliticaFeriasServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    private static (PoliticaFeriasService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var service = new PoliticaFeriasService(context, new FakeTimeProvider(Agora), NullLogger<PoliticaFeriasService>.Instance);
        return (service, context);
    }

    private static CreatePoliticaFeriasDto CriarDtoValido() => new()
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
        DiasMinimosAntesFeriadoOuFimDeSemana = 2,
        Ativa = true,
    };

    [Fact]
    public async Task CreateAsync_DeveCriarComValoresDaPlanilhaReal()
    {
        var (service, _) = CriarService();

        var politica = await service.CreateAsync(CriarDtoValido());

        Assert.Equal(UsuarioTipo.Pj, politica.TipoVinculo);
        Assert.Equal(30, politica.DiasDireitoPorAno);
        Assert.Equal(3, politica.MaxFracionamentos);
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarTipoVinculoInvalido()
    {
        var (service, _) = CriarService();
        var dto = CriarDtoValido();
        dto.TipoVinculo = "Autonomo";

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarUltimoFracionamentoMenorQueDemais()
    {
        var (service, _) = CriarService();
        var dto = CriarDtoValido();
        dto.DiasMinimoUltimoFracionamento = 3;
        dto.DiasMinimoDemaisFracionamentos = 5;

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(dto));
    }

    [Fact]
    public async Task UpdateAsync_DeveAtualizarParametros()
    {
        var (service, _) = CriarService();
        var politica = await service.CreateAsync(CriarDtoValido());

        var dto = new UpdatePoliticaFeriasDto
        {
            TipoVinculo = UsuarioTipo.Pj,
            DiasDireitoPorAno = 30,
            MaxFracionamentos = 2,
            DiasMinimoUltimoFracionamento = 20,
            DiasMinimoDemaisFracionamentos = 10,
            DiasAntecedenciaRemarcacao = 45,
            DiasAntecedenciaMarcacaoCompulsoria = 30,
            PermiteAbonoPecuniario = false,
            MaxDiasAbono = 0,
            DiasMinimosAntesFeriadoOuFimDeSemana = 2,
            Ativa = true,
        };

        var atualizada = await service.UpdateAsync(politica.Id, dto);

        Assert.Equal(2, atualizada.MaxFracionamentos);
        Assert.False(atualizada.PermiteAbonoPecuniario);
    }
}

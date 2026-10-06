using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoftwareLicense.Api.Data;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Entities;
using SoftwareLicense.Api.Exceptions;
using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class UnidadeOrcamentariaServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    private static (UnidadeOrcamentariaService Service, AppDbContext Context) CriarService()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var service = new UnidadeOrcamentariaService(context, new FakeTimeProvider(Agora), NullLogger<UnidadeOrcamentariaService>.Instance);
        return (service, context);
    }

    private static Setor CriarSetor(AppDbContext context, string nome = "GERÊNCIA ADMINISTRATIVA")
    {
        var setor = new Setor { Nome = nome, Ativo = true, DataCriacao = Agora.UtcDateTime, DataAtualizacao = Agora.UtcDateTime };
        context.Setores.Add(setor);
        context.SaveChanges();
        return setor;
    }

    private static CreateUnidadeOrcamentariaDto CriarDtoValido(int setorId, string codigo = "HP.ADM.2001") => new()
    {
        SetorId = setorId,
        Codigo = codigo,
        Descricao = "ADMINISTRATIVO",
        Apropriacao = "Custo do salário da equipe.",
    };

    [Fact]
    public async Task CreateAsync_DeveCriarComAtivaPadraoVerdadeiro()
    {
        var (service, context) = CriarService();
        var setor = CriarSetor(context);

        var unidade = await service.CreateAsync(CriarDtoValido(setor.Id));

        Assert.True(unidade.Ativa);
        Assert.Equal("HP.ADM.2001", unidade.Codigo);
        Assert.Equal(setor.Nome, unidade.SetorNome);
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarSetorInexistente()
    {
        var (service, _) = CriarService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAsync(CriarDtoValido(999)));
    }

    [Fact]
    public async Task CreateAsync_DeveRejeitarCodigoDuplicado()
    {
        var (service, context) = CriarService();
        var setor = CriarSetor(context);
        await service.CreateAsync(CriarDtoValido(setor.Id));

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(CriarDtoValido(setor.Id)));
    }

    [Fact]
    public async Task UpdateAsync_DevePermitirManterOMesmoCodigo()
    {
        var (service, context) = CriarService();
        var setor = CriarSetor(context);
        var unidade = await service.CreateAsync(CriarDtoValido(setor.Id));

        var atualizada = await service.UpdateAsync(unidade.Id, new UpdateUnidadeOrcamentariaDto
        {
            SetorId = setor.Id,
            Codigo = unidade.Codigo,
            Descricao = "ADMINISTRATIVO ATUALIZADO",
            Ativa = false,
        });

        Assert.Equal("ADMINISTRATIVO ATUALIZADO", atualizada.Descricao);
        Assert.False(atualizada.Ativa);
    }

    [Fact]
    public async Task GetAllAsync_DeveFiltrarPorSetorId()
    {
        var (service, context) = CriarService();
        var setorAdm = CriarSetor(context, "GERÊNCIA ADMINISTRATIVA");
        var setorEng = CriarSetor(context, "GERÊNCIA DE ENGENHARIA");
        await service.CreateAsync(CriarDtoValido(setorAdm.Id, "HP.ADM.2001"));
        await service.CreateAsync(CriarDtoValido(setorEng.Id, "HP.ENG.2001"));

        var resultado = await service.GetAllAsync(new UnidadeOrcamentariaFiltroDto { SetorId = setorEng.Id });

        Assert.Single(resultado);
        Assert.Equal("HP.ENG.2001", resultado[0].Codigo);
    }
    [Fact]
    public async Task GetAllAsync_DeveBuscarDescricaoPorParteIgnorandoAcentoECaixaEAceitandoCuringa()
    {
        var (service, context) = CriarService();
        var setor = CriarSetor(context, "GERÊNCIA ADMINISTRATIVA");
        foreach (var (codigo, descricao) in new[] { ("HP.RH.2001", "SALÁRIOS E ENCARGOS"), ("HP.RH.2002", "VALE TRANSPORTE"), ("HP.RH.2003", "Salario mínimo") })
        {
            var dto = CriarDtoValido(setor.Id, codigo);
            dto.Descricao = descricao;
            await service.CreateAsync(dto);
        }

        var comAsteriscos = await service.GetAllAsync(new UnidadeOrcamentariaFiltroDto { Descricao = "*salário*" });
        var semAcento = await service.GetAllAsync(new UnidadeOrcamentariaFiltroDto { Descricao = "SALARIO" });
        var curingaNoMeio = await service.GetAllAsync(new UnidadeOrcamentariaFiltroDto { Descricao = "salários*encargos" });
        var outraPalavra = await service.GetAllAsync(new UnidadeOrcamentariaFiltroDto { Descricao = "transporte" });

        Assert.Equal(2, comAsteriscos.Count);
        Assert.Equal(2, semAcento.Count);
        Assert.Equal("HP.RH.2001", Assert.Single(curingaNoMeio).Codigo);
        Assert.Equal("HP.RH.2002", Assert.Single(outraPalavra).Codigo);
    }
    [Fact]
    public async Task GetAllAsync_DeveBuscarPorApropriacaoIgnorandoAcentoECaixaEAceitandoCuringa()
    {
        var (service, context) = CriarService();
        var setor = CriarSetor(context, "GERÊNCIA ADMINISTRATIVA");
        foreach (var (codigo, apropriacao) in new[] { ("HP.RH.2001", "Salários e encargos da equipe"), ("HP.RH.2002", "Vale transporte"), ("HP.RH.2003", (string?)null) })
        {
            var dto = CriarDtoValido(setor.Id, codigo);
            dto.Apropriacao = apropriacao;
            await service.CreateAsync(dto);
        }

        var comAsteriscos = await service.GetAllAsync(new UnidadeOrcamentariaFiltroDto { Apropriacao = "*salario*" });
        var curingaNoMeio = await service.GetAllAsync(new UnidadeOrcamentariaFiltroDto { Apropriacao = "vale*porte" });

        Assert.Equal("HP.RH.2001", Assert.Single(comAsteriscos).Codigo);
        Assert.Equal("HP.RH.2002", Assert.Single(curingaNoMeio).Codigo);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Extensions;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/periodos-ferias")]
[Authorize(Roles = $"{Roles.Administrador},{Roles.Colaborador}")]
public class PeriodosFeriasController : ControllerBase
{
    private readonly IPeriodoFeriasService _periodoFeriasService;

    public PeriodosFeriasController(IPeriodoFeriasService periodoFeriasService)
    {
        _periodoFeriasService = periodoFeriasService;
    }

    [HttpGet]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<List<PeriodoFeriasDto>>> GetAll([FromQuery] PeriodoFeriasFiltroDto filtro)
    {
        var periodos = await _periodoFeriasService.GetAllAsync(filtro);
        return Ok(periodos);
    }

    // Usada pelo mobile - o colaborador vê o(s) próprio(s) período(s), com o saldo já calculado,
    // pra saber quantos dias pode pedir antes de solicitar uma programação.
    [HttpGet]
    [Route("/api/usuarios/{usuarioId:int}/periodos-ferias")]
    public async Task<ActionResult<List<PeriodoFeriasDto>>> GetByUsuario(int usuarioId)
    {
        if (!User.IsInRole(Roles.Administrador) && !User.TemUsuarioId(usuarioId))
        {
            return Forbid();
        }

        var periodos = await _periodoFeriasService.GetAllAsync(new PeriodoFeriasFiltroDto { UsuarioId = usuarioId });
        return Ok(periodos);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<PeriodoFeriasDto>> GetById(int id)
    {
        var periodo = await _periodoFeriasService.GetByIdAsync(id);
        return Ok(periodo);
    }

    [HttpGet("{id:int}/movimentacoes")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<List<MovimentacaoSaldoFeriasDto>>> GetMovimentacoes(int id)
    {
        var movimentacoes = await _periodoFeriasService.GetMovimentacoesAsync(id);
        return Ok(movimentacoes);
    }

    [HttpPost("{id:int}/ajustes-manuais")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<PeriodoFeriasDto>> RegistrarAjusteManual(int id, CreateAjusteManualSaldoFeriasDto dto)
    {
        var periodo = await _periodoFeriasService.RegistrarAjusteManualAsync(id, dto, User.ObterUsuarioId());
        return Ok(periodo);
    }

    [HttpPost]
    [Route("/api/usuarios/{usuarioId:int}/periodos-ferias/gerar-proximo")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<PeriodoFeriasDto>> GerarProximoPeriodo(int usuarioId)
    {
        var periodo = await _periodoFeriasService.GerarProximoPeriodoAsync(usuarioId, User.ObterUsuarioId());
        return Ok(periodo);
    }
}

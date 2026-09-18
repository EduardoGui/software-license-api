using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Extensions;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/periodos-ferias")]
[Authorize(Roles = Roles.Administrador)]
public class PeriodosFeriasController : ControllerBase
{
    private readonly IPeriodoFeriasService _periodoFeriasService;

    public PeriodosFeriasController(IPeriodoFeriasService periodoFeriasService)
    {
        _periodoFeriasService = periodoFeriasService;
    }

    [HttpGet]
    public async Task<ActionResult<List<PeriodoFeriasDto>>> GetAll([FromQuery] PeriodoFeriasFiltroDto filtro)
    {
        var periodos = await _periodoFeriasService.GetAllAsync(filtro);
        return Ok(periodos);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PeriodoFeriasDto>> GetById(int id)
    {
        var periodo = await _periodoFeriasService.GetByIdAsync(id);
        return Ok(periodo);
    }

    [HttpGet("{id:int}/movimentacoes")]
    public async Task<ActionResult<List<MovimentacaoSaldoFeriasDto>>> GetMovimentacoes(int id)
    {
        var movimentacoes = await _periodoFeriasService.GetMovimentacoesAsync(id);
        return Ok(movimentacoes);
    }

    [HttpPost("{id:int}/ajustes-manuais")]
    public async Task<ActionResult<PeriodoFeriasDto>> RegistrarAjusteManual(int id, CreateAjusteManualSaldoFeriasDto dto)
    {
        var periodo = await _periodoFeriasService.RegistrarAjusteManualAsync(id, dto, User.ObterUsuarioId());
        return Ok(periodo);
    }

    [HttpPost]
    [Route("/api/usuarios/{usuarioId:int}/periodos-ferias/gerar-proximo")]
    public async Task<ActionResult<PeriodoFeriasDto>> GerarProximoPeriodo(int usuarioId)
    {
        var periodo = await _periodoFeriasService.GerarProximoPeriodoAsync(usuarioId, User.ObterUsuarioId());
        return Ok(periodo);
    }
}

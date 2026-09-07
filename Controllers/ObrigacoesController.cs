using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/obrigacoes")]
[Authorize(Roles = Roles.Administrador)]
public class ObrigacoesController : ControllerBase
{
    private readonly IObrigacaoService _obrigacaoService;

    public ObrigacoesController(IObrigacaoService obrigacaoService)
    {
        _obrigacaoService = obrigacaoService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ObrigacaoDto>>> GetAll([FromQuery] ObrigacaoFiltroDto filtro)
    {
        var obrigacoes = await _obrigacaoService.GetAllAsync(filtro);
        return Ok(obrigacoes);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ObrigacaoDto>> GetById(int id)
    {
        var obrigacao = await _obrigacaoService.GetByIdAsync(id);
        return Ok(obrigacao);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ObrigacaoDto>> Update(int id, UpdateObrigacaoDto dto)
    {
        var obrigacao = await _obrigacaoService.UpdateAsync(id, dto);
        return Ok(obrigacao);
    }

    [HttpPatch("{id:int}/marcar-paga")]
    public async Task<ActionResult<ObrigacaoDto>> MarcarPaga(int id)
    {
        var obrigacao = await _obrigacaoService.MarcarPagaAsync(id);
        return Ok(obrigacao);
    }

    [HttpPatch("{id:int}/desmarcar-paga")]
    public async Task<ActionResult<ObrigacaoDto>> DesmarcarPaga(int id)
    {
        var obrigacao = await _obrigacaoService.DesmarcarPagaAsync(id);
        return Ok(obrigacao);
    }

    [HttpPatch("{id:int}/cancelar")]
    public async Task<ActionResult<ObrigacaoDto>> Cancelar(int id)
    {
        var obrigacao = await _obrigacaoService.CancelarAsync(id);
        return Ok(obrigacao);
    }
}

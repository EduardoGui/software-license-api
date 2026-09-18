using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Extensions;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/recessos-corporativos")]
[Authorize(Roles = Roles.Administrador)]
public class RecessosCorporativosController : ControllerBase
{
    private readonly IRecessoCorporativoService _recessoCorporativoService;

    public RecessosCorporativosController(IRecessoCorporativoService recessoCorporativoService)
    {
        _recessoCorporativoService = recessoCorporativoService;
    }

    [HttpGet]
    public async Task<ActionResult<List<RecessoCorporativoDto>>> GetAll()
    {
        var recessos = await _recessoCorporativoService.GetAllAsync();
        return Ok(recessos);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RecessoCorporativoDto>> GetById(int id)
    {
        var recesso = await _recessoCorporativoService.GetByIdAsync(id);
        return Ok(recesso);
    }

    [HttpPost]
    public async Task<ActionResult<RecessoCorporativoDto>> Create(CreateRecessoCorporativoDto dto)
    {
        var recesso = await _recessoCorporativoService.CreateAsync(dto, User.ObterUsuarioId());
        return CreatedAtAction(nameof(GetById), new { id = recesso.Id }, recesso);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<RecessoCorporativoDto>> Update(int id, UpdateRecessoCorporativoDto dto)
    {
        var recesso = await _recessoCorporativoService.UpdateAsync(id, dto, User.ObterUsuarioId());
        return Ok(recesso);
    }

    [HttpPost("{id:int}/simular")]
    public async Task<ActionResult<List<RecessoSimulacaoLinhaDto>>> Simular(int id, SimularRecessoDto dto)
    {
        var linhas = await _recessoCorporativoService.SimularAsync(id, dto);
        return Ok(linhas);
    }

    [HttpPost("{id:int}/confirmar")]
    public async Task<ActionResult<RecessoCorporativoDto>> Confirmar(int id, SimularRecessoDto dto)
    {
        var recesso = await _recessoCorporativoService.ConfirmarAsync(id, dto, User.ObterUsuarioId());
        return Ok(recesso);
    }
}

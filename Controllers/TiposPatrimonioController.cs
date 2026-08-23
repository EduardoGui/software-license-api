using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/tipos-patrimonio")]
[Authorize(Roles = Roles.Administrador)]
public class TiposPatrimonioController : ControllerBase
{
    private readonly ITipoPatrimonioService _tipoPatrimonioService;

    public TiposPatrimonioController(ITipoPatrimonioService tipoPatrimonioService)
    {
        _tipoPatrimonioService = tipoPatrimonioService;
    }

    [HttpGet]
    public async Task<ActionResult<List<TipoPatrimonioDto>>> GetAll([FromQuery] TipoPatrimonioFiltroDto filtro)
    {
        var tipos = await _tipoPatrimonioService.GetAllAsync(filtro);
        return Ok(tipos);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TipoPatrimonioDto>> GetById(int id)
    {
        var tipo = await _tipoPatrimonioService.GetByIdAsync(id);
        return Ok(tipo);
    }

    [HttpPost]
    public async Task<ActionResult<TipoPatrimonioDto>> Create(CreateTipoPatrimonioDto dto)
    {
        var tipo = await _tipoPatrimonioService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = tipo.Id }, tipo);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TipoPatrimonioDto>> Update(int id, UpdateTipoPatrimonioDto dto)
    {
        var tipo = await _tipoPatrimonioService.UpdateAsync(id, dto);
        return Ok(tipo);
    }
}

using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/tipos-equipamento")]
public class TiposEquipamentoController : ControllerBase
{
    private readonly ITipoEquipamentoService _tipoEquipamentoService;

    public TiposEquipamentoController(ITipoEquipamentoService tipoEquipamentoService)
    {
        _tipoEquipamentoService = tipoEquipamentoService;
    }

    [HttpGet]
    public async Task<ActionResult<List<TipoEquipamentoDto>>> GetAll([FromQuery] TipoEquipamentoFiltroDto filtro)
    {
        var tipos = await _tipoEquipamentoService.GetAllAsync(filtro);
        return Ok(tipos);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TipoEquipamentoDto>> GetById(int id)
    {
        var tipo = await _tipoEquipamentoService.GetByIdAsync(id);
        return Ok(tipo);
    }

    [HttpPost]
    public async Task<ActionResult<TipoEquipamentoDto>> Create(CreateTipoEquipamentoDto dto)
    {
        var tipo = await _tipoEquipamentoService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = tipo.Id }, tipo);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TipoEquipamentoDto>> Update(int id, UpdateTipoEquipamentoDto dto)
    {
        var tipo = await _tipoEquipamentoService.UpdateAsync(id, dto);
        return Ok(tipo);
    }
}

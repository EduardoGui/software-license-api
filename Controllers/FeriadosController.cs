using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/feriados")]
[Authorize(Roles = Roles.Administrador)]
public class FeriadosController : ControllerBase
{
    private readonly IFeriadoService _feriadoService;

    public FeriadosController(IFeriadoService feriadoService)
    {
        _feriadoService = feriadoService;
    }

    [HttpGet]
    public async Task<ActionResult<List<FeriadoDto>>> GetAll([FromQuery] FeriadoFiltroDto filtro)
    {
        var feriados = await _feriadoService.GetAllAsync(filtro);
        return Ok(feriados);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<FeriadoDto>> GetById(int id)
    {
        var feriado = await _feriadoService.GetByIdAsync(id);
        return Ok(feriado);
    }

    [HttpPost]
    public async Task<ActionResult<FeriadoDto>> Create(CreateFeriadoDto dto)
    {
        var feriado = await _feriadoService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = feriado.Id }, feriado);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<FeriadoDto>> Update(int id, UpdateFeriadoDto dto)
    {
        var feriado = await _feriadoService.UpdateAsync(id, dto);
        return Ok(feriado);
    }
}

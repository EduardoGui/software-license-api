using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/unidades-orcamentarias")]
[Authorize(Roles = Roles.Administrador)]
public class UnidadesOrcamentariasController : ControllerBase
{
    private readonly IUnidadeOrcamentariaService _unidadeOrcamentariaService;

    public UnidadesOrcamentariasController(IUnidadeOrcamentariaService unidadeOrcamentariaService)
    {
        _unidadeOrcamentariaService = unidadeOrcamentariaService;
    }

    [HttpGet]
    public async Task<ActionResult<List<UnidadeOrcamentariaDto>>> GetAll([FromQuery] UnidadeOrcamentariaFiltroDto filtro)
    {
        var unidades = await _unidadeOrcamentariaService.GetAllAsync(filtro);
        return Ok(unidades);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UnidadeOrcamentariaDto>> GetById(int id)
    {
        var unidade = await _unidadeOrcamentariaService.GetByIdAsync(id);
        return Ok(unidade);
    }

    [HttpPost]
    public async Task<ActionResult<UnidadeOrcamentariaDto>> Create(CreateUnidadeOrcamentariaDto dto)
    {
        var unidade = await _unidadeOrcamentariaService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = unidade.Id }, unidade);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<UnidadeOrcamentariaDto>> Update(int id, UpdateUnidadeOrcamentariaDto dto)
    {
        var unidade = await _unidadeOrcamentariaService.UpdateAsync(id, dto);
        return Ok(unidade);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/politicas-ferias")]
[Authorize(Roles = Roles.Administrador)]
public class PoliticasFeriasController : ControllerBase
{
    private readonly IPoliticaFeriasService _politicaFeriasService;

    public PoliticasFeriasController(IPoliticaFeriasService politicaFeriasService)
    {
        _politicaFeriasService = politicaFeriasService;
    }

    [HttpGet]
    public async Task<ActionResult<List<PoliticaFeriasDto>>> GetAll()
    {
        var politicas = await _politicaFeriasService.GetAllAsync();
        return Ok(politicas);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PoliticaFeriasDto>> GetById(int id)
    {
        var politica = await _politicaFeriasService.GetByIdAsync(id);
        return Ok(politica);
    }

    [HttpPost]
    public async Task<ActionResult<PoliticaFeriasDto>> Create(CreatePoliticaFeriasDto dto)
    {
        var politica = await _politicaFeriasService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = politica.Id }, politica);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PoliticaFeriasDto>> Update(int id, UpdatePoliticaFeriasDto dto)
    {
        var politica = await _politicaFeriasService.UpdateAsync(id, dto);
        return Ok(politica);
    }
}

using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/licencas")]
public class LicencasController : ControllerBase
{
    private readonly ILicencaService _licencaService;

    public LicencasController(ILicencaService licencaService)
    {
        _licencaService = licencaService;
    }

    [HttpGet]
    public async Task<ActionResult<List<LicencaDto>>> GetAll([FromQuery] LicencaFiltroDto filtro)
    {
        var licencas = await _licencaService.GetAllAsync(filtro);
        return Ok(licencas);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LicencaDto>> GetById(int id)
    {
        var licenca = await _licencaService.GetByIdAsync(id);
        return Ok(licenca);
    }

    [HttpPost]
    public async Task<ActionResult<LicencaDto>> Create(CreateLicencaDto dto)
    {
        var licenca = await _licencaService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = licenca.Id }, licenca);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<LicencaDto>> Update(int id, UpdateLicencaDto dto)
    {
        var licenca = await _licencaService.UpdateAsync(id, dto);
        return Ok(licenca);
    }

    [HttpPatch("{id:int}/desativar")]
    public async Task<ActionResult<LicencaDto>> Desativar(int id)
    {
        var licenca = await _licencaService.DesativarAsync(id);
        return Ok(licenca);
    }
}

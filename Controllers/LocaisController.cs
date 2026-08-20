using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/locais")]
public class LocaisController : ControllerBase
{
    private readonly ILocalService _localService;

    public LocaisController(ILocalService localService)
    {
        _localService = localService;
    }

    // Leitura liberada para Colaborador também: é dado de referência usado no formulário de
    // Reembolso de Despesa (seleção do local/obra).
    [HttpGet]
    [Authorize(Roles = $"{Roles.Administrador},{Roles.Colaborador}")]
    public async Task<ActionResult<List<LocalDto>>> GetAll([FromQuery] LocalFiltroDto filtro)
    {
        var locais = await _localService.GetAllAsync(filtro);
        return Ok(locais);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = $"{Roles.Administrador},{Roles.Colaborador}")]
    public async Task<ActionResult<LocalDto>> GetById(int id)
    {
        var local = await _localService.GetByIdAsync(id);
        return Ok(local);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<LocalDto>> Create(CreateLocalDto dto)
    {
        var local = await _localService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = local.Id }, local);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<LocalDto>> Update(int id, UpdateLocalDto dto)
    {
        var local = await _localService.UpdateAsync(id, dto);
        return Ok(local);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/tipos-despesa")]
public class TiposDespesaController : ControllerBase
{
    private readonly ITipoDespesaService _tipoDespesaService;

    public TiposDespesaController(ITipoDespesaService tipoDespesaService)
    {
        _tipoDespesaService = tipoDespesaService;
    }

    // Leitura liberada para Colaborador também: é dado de referência usado no formulário de
    // Reembolso de Despesa (seleção do tipo de cada item).
    [HttpGet]
    [Authorize(Roles = $"{Roles.Administrador},{Roles.Colaborador}")]
    public async Task<ActionResult<List<TipoDespesaDto>>> GetAll([FromQuery] TipoDespesaFiltroDto filtro)
    {
        var tipos = await _tipoDespesaService.GetAllAsync(filtro);
        return Ok(tipos);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = $"{Roles.Administrador},{Roles.Colaborador}")]
    public async Task<ActionResult<TipoDespesaDto>> GetById(int id)
    {
        var tipo = await _tipoDespesaService.GetByIdAsync(id);
        return Ok(tipo);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<TipoDespesaDto>> Create(CreateTipoDespesaDto dto)
    {
        var tipo = await _tipoDespesaService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = tipo.Id }, tipo);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<TipoDespesaDto>> Update(int id, UpdateTipoDespesaDto dto)
    {
        var tipo = await _tipoDespesaService.UpdateAsync(id, dto);
        return Ok(tipo);
    }
}

using Microsoft.AspNetCore.Authorization;
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

    // Leitura liberada ao Administrativo: a tela de Nota Fiscal de Entrada (que ele acessa) precisa
    // da lista para escolher o tipo do equipamento ao adicionar o item. Criar/editar segue só do Administrador.
    [HttpGet]
    [Authorize(Roles = $"{Roles.Administrador},{Roles.Administrativo}")]
    public async Task<ActionResult<List<TipoEquipamentoDto>>> GetAll([FromQuery] TipoEquipamentoFiltroDto filtro)
    {
        var tipos = await _tipoEquipamentoService.GetAllAsync(filtro);
        return Ok(tipos);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = $"{Roles.Administrador},{Roles.Administrativo}")]
    public async Task<ActionResult<TipoEquipamentoDto>> GetById(int id)
    {
        var tipo = await _tipoEquipamentoService.GetByIdAsync(id);
        return Ok(tipo);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<TipoEquipamentoDto>> Create(CreateTipoEquipamentoDto dto)
    {
        var tipo = await _tipoEquipamentoService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = tipo.Id }, tipo);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<TipoEquipamentoDto>> Update(int id, UpdateTipoEquipamentoDto dto)
    {
        var tipo = await _tipoEquipamentoService.UpdateAsync(id, dto);
        return Ok(tipo);
    }
}

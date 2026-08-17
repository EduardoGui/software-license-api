using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/equipamentos")]
public class EquipamentosController : ControllerBase
{
    private readonly IEquipamentoService _equipamentoService;

    public EquipamentosController(IEquipamentoService equipamentoService)
    {
        _equipamentoService = equipamentoService;
    }

    [HttpGet]
    public async Task<ActionResult<List<EquipamentoDto>>> GetAll([FromQuery] EquipamentoFiltroDto filtro)
    {
        var equipamentos = await _equipamentoService.GetAllAsync(filtro);
        return Ok(equipamentos);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EquipamentoDto>> GetById(int id)
    {
        var equipamento = await _equipamentoService.GetByIdAsync(id);
        return Ok(equipamento);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<EquipamentoDto>> Update(int id, UpdateEquipamentoDto dto)
    {
        var equipamento = await _equipamentoService.UpdateAsync(id, dto);
        return Ok(equipamento);
    }

    [HttpPatch("{id:int}/baixar")]
    public async Task<ActionResult<EquipamentoDto>> Baixar(int id)
    {
        var equipamento = await _equipamentoService.BaixarAsync(id);
        return Ok(equipamento);
    }
}

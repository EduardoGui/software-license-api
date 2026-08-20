using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/setores")]
public class SetoresController : ControllerBase
{
    private readonly ISetorService _setorService;

    public SetoresController(ISetorService setorService)
    {
        _setorService = setorService;
    }

    // Leitura liberada para Colaborador também: é dado de referência usado na tela de "Meus
    // dados bancários" (seleção do próprio setor) e no formulário de Reembolso de Despesa.
    [HttpGet]
    [Authorize(Roles = $"{Roles.Administrador},{Roles.Colaborador}")]
    public async Task<ActionResult<List<SetorDto>>> GetAll([FromQuery] SetorFiltroDto filtro)
    {
        var setores = await _setorService.GetAllAsync(filtro);
        return Ok(setores);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = $"{Roles.Administrador},{Roles.Colaborador}")]
    public async Task<ActionResult<SetorDto>> GetById(int id)
    {
        var setor = await _setorService.GetByIdAsync(id);
        return Ok(setor);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<SetorDto>> Create(CreateSetorDto dto)
    {
        var setor = await _setorService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = setor.Id }, setor);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<SetorDto>> Update(int id, UpdateSetorDto dto)
    {
        var setor = await _setorService.UpdateAsync(id, dto);
        return Ok(setor);
    }

    [HttpPost("{id:int}/aprovadores")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<SetorDto>> AdicionarAprovador(int id, CreateSetorAprovadorDto dto)
    {
        var setor = await _setorService.AdicionarAprovadorAsync(id, dto);
        return Ok(setor);
    }

    [HttpDelete("{id:int}/aprovadores/{aprovadorId:int}")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<SetorDto>> RemoverAprovador(int id, int aprovadorId)
    {
        var setor = await _setorService.RemoverAprovadorAsync(id, aprovadorId);
        return Ok(setor);
    }
}

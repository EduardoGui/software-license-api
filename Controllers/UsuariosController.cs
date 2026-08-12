using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/usuarios")]
public class UsuariosController : ControllerBase
{
    private readonly IUsuarioService _usuarioService;

    public UsuariosController(IUsuarioService usuarioService)
    {
        _usuarioService = usuarioService;
    }

    [HttpGet]
    public async Task<ActionResult<List<UsuarioDto>>> GetAll([FromQuery] UsuarioFiltroDto filtro)
    {
        var usuarios = await _usuarioService.GetAllAsync(filtro);
        return Ok(usuarios);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UsuarioDto>> GetById(int id)
    {
        var usuario = await _usuarioService.GetByIdAsync(id);
        return Ok(usuario);
    }

    [HttpPost]
    public async Task<ActionResult<UsuarioDto>> Create(CreateUsuarioDto dto)
    {
        var usuario = await _usuarioService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = usuario.Id }, usuario);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<UsuarioDto>> Update(int id, UpdateUsuarioDto dto)
    {
        var usuario = await _usuarioService.UpdateAsync(id, dto);
        return Ok(usuario);
    }

    [HttpPatch("{id:int}/desativar")]
    public async Task<ActionResult<UsuarioDto>> Desativar(int id, DesativarUsuarioDto dto)
    {
        var usuario = await _usuarioService.DesativarAsync(id, dto);
        return Ok(usuario);
    }
}

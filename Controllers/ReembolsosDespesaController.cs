using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Extensions;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/reembolsos-despesa")]
[Authorize(Roles = $"{Roles.Administrador},{Roles.Colaborador}")]
public class ReembolsosDespesaController : ControllerBase
{
    private readonly IReembolsoDespesaService _reembolsoDespesaService;

    public ReembolsosDespesaController(IReembolsoDespesaService reembolsoDespesaService)
    {
        _reembolsoDespesaService = reembolsoDespesaService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ReembolsoDespesaDto>>> GetAll([FromQuery] ReembolsoDespesaFiltroDto filtro)
    {
        if (!User.IsInRole(Roles.Administrador) && (filtro.UsuarioId is null || !User.TemUsuarioId(filtro.UsuarioId.Value)))
        {
            return Forbid();
        }

        var reembolsos = await _reembolsoDespesaService.GetAllAsync(filtro);
        return Ok(reembolsos);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ReembolsoDespesaDto>> GetById(int id)
    {
        var reembolso = await _reembolsoDespesaService.GetByIdAsync(id);

        if (!User.IsInRole(Roles.Administrador) && !User.TemUsuarioId(reembolso.UsuarioId))
        {
            return Forbid();
        }

        return Ok(reembolso);
    }

    [HttpPost]
    public async Task<ActionResult<ReembolsoDespesaDto>> Create(CreateReembolsoDespesaDto dto)
    {
        var usuarioId = User.ObterUsuarioId();
        if (usuarioId is null)
        {
            return Forbid();
        }

        var reembolso = await _reembolsoDespesaService.CreateAsync(usuarioId.Value, dto);
        return CreatedAtAction(nameof(GetById), new { id = reembolso.Id }, reembolso);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ReembolsoDespesaDto>> Update(int id, UpdateReembolsoDespesaDto dto)
    {
        var existente = await _reembolsoDespesaService.GetByIdAsync(id);
        if (!User.IsInRole(Roles.Administrador) && !User.TemUsuarioId(existente.UsuarioId))
        {
            return Forbid();
        }

        var reembolso = await _reembolsoDespesaService.UpdateAsync(id, dto, User.ObterUsuarioId());
        return Ok(reembolso);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Excluir(int id)
    {
        var existente = await _reembolsoDespesaService.GetByIdAsync(id);
        if (!User.IsInRole(Roles.Administrador) && !User.TemUsuarioId(existente.UsuarioId))
        {
            return Forbid();
        }

        await _reembolsoDespesaService.ExcluirAsync(id, User.ObterUsuarioId());
        return NoContent();
    }

    [HttpPatch("{id:int}/enviar")]
    public async Task<ActionResult<ReembolsoDespesaDto>> Enviar(int id)
    {
        var existente = await _reembolsoDespesaService.GetByIdAsync(id);
        if (!User.IsInRole(Roles.Administrador) && !User.TemUsuarioId(existente.UsuarioId))
        {
            return Forbid();
        }

        var reembolso = await _reembolsoDespesaService.EnviarAsync(id, User.ObterUsuarioId());
        return Ok(reembolso);
    }

    [HttpGet("pendentes-aprovacao")]
    public async Task<ActionResult<List<ReembolsoDespesaDto>>> GetPendentesParaAprovacao()
    {
        var aprovadorUsuarioId = User.ObterUsuarioId();
        if (aprovadorUsuarioId is null)
        {
            return Forbid();
        }

        var reembolsos = await _reembolsoDespesaService.GetPendentesParaAprovacaoAsync(aprovadorUsuarioId.Value);
        return Ok(reembolsos);
    }

    [HttpPatch("{id:int}/aprovar")]
    public async Task<ActionResult<ReembolsoDespesaDto>> Aprovar(int id)
    {
        var aprovadorUsuarioId = User.ObterUsuarioId();
        if (aprovadorUsuarioId is null)
        {
            return Forbid();
        }

        var reembolso = await _reembolsoDespesaService.AprovarAsync(id, aprovadorUsuarioId.Value);
        return Ok(reembolso);
    }

    [HttpPatch("{id:int}/devolver")]
    public async Task<ActionResult<ReembolsoDespesaDto>> Devolver(int id, DevolverReembolsoDespesaDto dto)
    {
        var aprovadorUsuarioId = User.ObterUsuarioId();
        if (aprovadorUsuarioId is null)
        {
            return Forbid();
        }

        var reembolso = await _reembolsoDespesaService.DevolverAsync(id, aprovadorUsuarioId.Value, dto);
        return Ok(reembolso);
    }

    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> GerarPdf(int id)
    {
        var existente = await _reembolsoDespesaService.GetByIdAsync(id);
        if (!User.IsInRole(Roles.Administrador) && !User.TemUsuarioId(existente.UsuarioId))
        {
            return Forbid();
        }

        var pdf = await _reembolsoDespesaService.GerarPdfAsync(id);
        return File(pdf, "application/pdf", $"reembolso-{existente.Numero}.pdf");
    }

    [HttpPatch("{id:int}/reprovar")]
    public async Task<ActionResult<ReembolsoDespesaDto>> Reprovar(int id, ReprovarReembolsoDespesaDto dto)
    {
        var aprovadorUsuarioId = User.ObterUsuarioId();
        if (aprovadorUsuarioId is null)
        {
            return Forbid();
        }

        var reembolso = await _reembolsoDespesaService.ReprovarAsync(id, aprovadorUsuarioId.Value, dto);
        return Ok(reembolso);
    }
}

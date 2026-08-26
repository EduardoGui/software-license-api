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

        if (!await PodeVisualizarAsync(reembolso))
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

    [HttpGet("aprovados-por-mim")]
    public async Task<ActionResult<List<ReembolsoDespesaDto>>> GetAprovadosPorMim()
    {
        var aprovadorUsuarioId = User.ObterUsuarioId();
        if (aprovadorUsuarioId is null)
        {
            return Forbid();
        }

        var reembolsos = await _reembolsoDespesaService.GetAprovadosPorMimAsync(aprovadorUsuarioId.Value);
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
        if (!await PodeVisualizarAsync(existente))
        {
            return Forbid();
        }

        var pdf = await _reembolsoDespesaService.GerarPdfAsync(id);
        return File(pdf, "application/pdf", $"reembolso-{existente.Numero}.pdf");
    }

    [HttpGet("{id:int}/itens/{itemId:int}/anexos")]
    public async Task<ActionResult<List<AnexoDto>>> ListarAnexosItem(int id, int itemId)
    {
        var existente = await _reembolsoDespesaService.GetByIdAsync(id);
        if (!await PodeVisualizarAsync(existente))
        {
            return Forbid();
        }

        var anexos = await _reembolsoDespesaService.ListarAnexosItemAsync(id, itemId);
        return Ok(anexos);
    }

    [HttpPost("{id:int}/itens/{itemId:int}/anexos")]
    public async Task<ActionResult<AnexoDto>> AdicionarAnexoItem(int id, int itemId, IFormFile arquivo)
    {
        var existente = await _reembolsoDespesaService.GetByIdAsync(id);
        if (!User.IsInRole(Roles.Administrador) && !User.TemUsuarioId(existente.UsuarioId))
        {
            return Forbid();
        }

        if (arquivo is null || arquivo.Length == 0)
        {
            return BadRequest(new { message = "Nenhum arquivo enviado." });
        }

        using var stream = new MemoryStream();
        await arquivo.CopyToAsync(stream);

        var anexo = await _reembolsoDespesaService.AdicionarAnexoItemAsync(id, itemId, new AdicionarAnexoDto
        {
            NomeArquivo = arquivo.FileName,
            TipoConteudo = arquivo.ContentType,
            Conteudo = stream.ToArray(),
        }, User.ObterUsuarioId());

        return Ok(anexo);
    }

    [HttpGet("{id:int}/itens/{itemId:int}/anexos/{anexoId:int}")]
    public async Task<IActionResult> BaixarAnexoItem(int id, int itemId, int anexoId)
    {
        var existente = await _reembolsoDespesaService.GetByIdAsync(id);
        if (!await PodeVisualizarAsync(existente))
        {
            return Forbid();
        }

        var arquivo = await _reembolsoDespesaService.ObterAnexoItemAsync(id, itemId, anexoId);
        return File(arquivo.Conteudo, arquivo.TipoConteudo, arquivo.NomeArquivo);
    }

    [HttpDelete("{id:int}/itens/{itemId:int}/anexos/{anexoId:int}")]
    public async Task<IActionResult> ExcluirAnexoItem(int id, int itemId, int anexoId)
    {
        var existente = await _reembolsoDespesaService.GetByIdAsync(id);
        if (!User.IsInRole(Roles.Administrador) && !User.TemUsuarioId(existente.UsuarioId))
        {
            return Forbid();
        }

        await _reembolsoDespesaService.ExcluirAnexoItemAsync(id, itemId, anexoId, User.ObterUsuarioId());
        return NoContent();
    }

    private async Task<bool> PodeVisualizarAsync(ReembolsoDespesaDto reembolso)
    {
        if (User.IsInRole(Roles.Administrador) || User.TemUsuarioId(reembolso.UsuarioId))
        {
            return true;
        }

        var usuarioId = User.ObterUsuarioId();
        return usuarioId is not null && await _reembolsoDespesaService.EhAprovadorDoSetorAsync(usuarioId.Value, reembolso.SetorId);
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

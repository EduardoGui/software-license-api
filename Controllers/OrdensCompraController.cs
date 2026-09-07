using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/ordens-compra")]
[Authorize(Roles = Roles.Administrador)]
public class OrdensCompraController : ControllerBase
{
    private readonly IOrdemCompraService _ordemCompraService;

    public OrdensCompraController(IOrdemCompraService ordemCompraService)
    {
        _ordemCompraService = ordemCompraService;
    }

    [HttpGet]
    public async Task<ActionResult<List<OrdemCompraDto>>> GetAll([FromQuery] OrdemCompraFiltroDto filtro)
    {
        var ordens = await _ordemCompraService.GetAllAsync(filtro);
        return Ok(ordens);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrdemCompraDetalheDto>> GetById(int id)
    {
        var ordemCompra = await _ordemCompraService.GetByIdAsync(id);
        return Ok(ordemCompra);
    }

    [HttpPost]
    public async Task<ActionResult<OrdemCompraDto>> Create(CreateOrdemCompraDto dto)
    {
        var ordemCompra = await _ordemCompraService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = ordemCompra.Id }, ordemCompra);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<OrdemCompraDto>> Update(int id, UpdateOrdemCompraDto dto)
    {
        var ordemCompra = await _ordemCompraService.UpdateAsync(id, dto);
        return Ok(ordemCompra);
    }

    [HttpPatch("{id:int}/emitir")]
    public async Task<ActionResult<OrdemCompraDto>> Emitir(int id)
    {
        var ordemCompra = await _ordemCompraService.EmitirAsync(id);
        return Ok(ordemCompra);
    }

    [HttpPatch("{id:int}/marcar-assinada")]
    public async Task<ActionResult<OrdemCompraDto>> MarcarAssinada(int id)
    {
        var ordemCompra = await _ordemCompraService.MarcarAssinadaAsync(id);
        return Ok(ordemCompra);
    }

    [HttpPatch("{id:int}/cancelar")]
    public async Task<ActionResult<OrdemCompraDto>> Cancelar(int id)
    {
        var ordemCompra = await _ordemCompraService.CancelarAsync(id);
        return Ok(ordemCompra);
    }

    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> GerarPdf(int id)
    {
        var pdf = await _ordemCompraService.GerarPdfAsync(id);
        return File(pdf, "application/pdf", $"OC-{id}.pdf");
    }

    [HttpGet("{id:int}/anexos")]
    public async Task<ActionResult<List<AnexoDto>>> ListarAnexos(int id)
    {
        var anexos = await _ordemCompraService.ListarAnexosAsync(id);
        return Ok(anexos);
    }

    [HttpPost("{id:int}/anexos")]
    public async Task<ActionResult<AnexoDto>> AdicionarAnexo(int id, IFormFile arquivo)
    {
        if (arquivo is null || arquivo.Length == 0)
        {
            return BadRequest(new { message = "Nenhum arquivo enviado." });
        }

        using var stream = new MemoryStream();
        await arquivo.CopyToAsync(stream);

        var anexo = await _ordemCompraService.AdicionarAnexoAsync(id, new AdicionarAnexoDto
        {
            NomeArquivo = arquivo.FileName,
            TipoConteudo = arquivo.ContentType,
            Conteudo = stream.ToArray(),
        });

        return Ok(anexo);
    }

    [HttpGet("{id:int}/anexos/{anexoId:int}")]
    public async Task<IActionResult> BaixarAnexo(int id, int anexoId)
    {
        var arquivo = await _ordemCompraService.ObterAnexoAsync(id, anexoId);
        return File(arquivo.Conteudo, arquivo.TipoConteudo, arquivo.NomeArquivo);
    }

    [HttpDelete("{id:int}/anexos/{anexoId:int}")]
    public async Task<IActionResult> ExcluirAnexo(int id, int anexoId)
    {
        await _ordemCompraService.ExcluirAnexoAsync(id, anexoId);
        return NoContent();
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/notas-fiscais-entrada")]
[Authorize(Roles = Roles.Administrador)]
public class NotasFiscaisEntradaController : ControllerBase
{
    private readonly INotaFiscalEntradaService _notaFiscalEntradaService;

    public NotasFiscaisEntradaController(INotaFiscalEntradaService notaFiscalEntradaService)
    {
        _notaFiscalEntradaService = notaFiscalEntradaService;
    }

    [HttpGet]
    public async Task<ActionResult<List<NotaFiscalEntradaDto>>> GetAll([FromQuery] NotaFiscalEntradaFiltroDto filtro)
    {
        var notas = await _notaFiscalEntradaService.GetAllAsync(filtro);
        return Ok(notas);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<NotaFiscalEntradaDetalheDto>> GetById(int id)
    {
        var nota = await _notaFiscalEntradaService.GetByIdAsync(id);
        return Ok(nota);
    }

    [HttpPost]
    public async Task<ActionResult<NotaFiscalEntradaDto>> Create(CreateNotaFiscalEntradaDto dto)
    {
        var nota = await _notaFiscalEntradaService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = nota.Id }, nota);
    }

    [HttpPost("{id:int}/itens")]
    public async Task<ActionResult<NotaFiscalItemDto>> AdicionarItem(int id, CreateNotaFiscalItemDto dto)
    {
        var item = await _notaFiscalEntradaService.AdicionarItemAsync(id, dto);
        return Ok(item);
    }

    [HttpGet("{id:int}/anexos")]
    public async Task<ActionResult<List<AnexoDto>>> ListarAnexos(int id)
    {
        var anexos = await _notaFiscalEntradaService.ListarAnexosAsync(id);
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

        var anexo = await _notaFiscalEntradaService.AdicionarAnexoAsync(id, new AdicionarAnexoDto
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
        var arquivo = await _notaFiscalEntradaService.ObterAnexoAsync(id, anexoId);
        return File(arquivo.Conteudo, arquivo.TipoConteudo, arquivo.NomeArquivo);
    }

    [HttpDelete("{id:int}/anexos/{anexoId:int}")]
    public async Task<IActionResult> ExcluirAnexo(int id, int anexoId)
    {
        await _notaFiscalEntradaService.ExcluirAnexoAsync(id, anexoId);
        return NoContent();
    }
}

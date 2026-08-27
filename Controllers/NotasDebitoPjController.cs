using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/notas-debito-pj")]
[Authorize(Roles = Roles.Administrador)]
public class NotasDebitoPjController : ControllerBase
{
    private readonly INotaDebitoPjService _notaDebitoPjService;

    public NotasDebitoPjController(INotaDebitoPjService notaDebitoPjService)
    {
        _notaDebitoPjService = notaDebitoPjService;
    }

    [HttpGet]
    public async Task<ActionResult<List<NotaDebitoPjDto>>> GetAll([FromQuery] NotaDebitoPjFiltroDto filtro)
    {
        var notas = await _notaDebitoPjService.GetAllAsync(filtro);
        return Ok(notas);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<NotaDebitoPjDto>> GetById(int id)
    {
        var nota = await _notaDebitoPjService.GetByIdAsync(id);
        return Ok(nota);
    }

    [HttpPost]
    public async Task<ActionResult<NotaDebitoPjDto>> Create(CreateNotaDebitoPjDto dto)
    {
        var nota = await _notaDebitoPjService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = nota.Id }, nota);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<NotaDebitoPjDto>> Update(int id, UpdateNotaDebitoPjDto dto)
    {
        var nota = await _notaDebitoPjService.UpdateAsync(id, dto);
        return Ok(nota);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _notaDebitoPjService.DeleteAsync(id);
        return NoContent();
    }

    [HttpPatch("{id:int}/enviar")]
    public async Task<ActionResult<NotaDebitoPjDto>> Enviar(int id)
    {
        var nota = await _notaDebitoPjService.EnviarAsync(id);
        return Ok(nota);
    }

    [HttpPatch("{id:int}/pagar")]
    public async Task<ActionResult<NotaDebitoPjDto>> Pagar(int id, PagarNotaDebitoPjDto dto)
    {
        var nota = await _notaDebitoPjService.PagarAsync(id, dto);
        return Ok(nota);
    }

    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> Pdf(int id)
    {
        var pdf = await _notaDebitoPjService.GerarPdfAsync(id);
        return File(pdf, "application/pdf", $"nota-debito-{id:D4}.pdf");
    }

    [HttpGet("{id:int}/anexos")]
    public async Task<ActionResult<List<AnexoDto>>> ListarAnexos(int id)
    {
        var anexos = await _notaDebitoPjService.ListarAnexosAsync(id);
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

        var anexo = await _notaDebitoPjService.AdicionarAnexoAsync(id, new AdicionarAnexoDto
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
        var arquivo = await _notaDebitoPjService.ObterAnexoAsync(id, anexoId);
        return File(arquivo.Conteudo, arquivo.TipoConteudo, arquivo.NomeArquivo);
    }

    [HttpDelete("{id:int}/anexos/{anexoId:int}")]
    public async Task<IActionResult> ExcluirAnexo(int id, int anexoId)
    {
        await _notaDebitoPjService.ExcluirAnexoAsync(id, anexoId);
        return NoContent();
    }
}

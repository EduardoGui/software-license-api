using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/patrimonio-itens")]
[Authorize(Roles = Roles.Administrador)]
public class PatrimonioItensController : ControllerBase
{
    private readonly IPatrimonioItemService _patrimonioItemService;

    public PatrimonioItensController(IPatrimonioItemService patrimonioItemService)
    {
        _patrimonioItemService = patrimonioItemService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PatrimonioItemFiltroDto filtro, [FromQuery] string? formato)
    {
        var itens = await _patrimonioItemService.GetAllAsync(filtro);

        if (string.Equals(formato, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            var arquivo = _patrimonioItemService.GerarExcel(itens);
            var nomeArquivo = $"patrimonio-{DateTime.Now:yyyyMMdd}.xlsx";
            return File(arquivo, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nomeArquivo);
        }

        return Ok(itens);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PatrimonioItemDto>> GetById(int id)
    {
        var item = await _patrimonioItemService.GetByIdAsync(id);
        return Ok(item);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PatrimonioItemDto>> Update(int id, UpdatePatrimonioItemDto dto)
    {
        var item = await _patrimonioItemService.UpdateAsync(id, dto);
        return Ok(item);
    }

    [HttpPatch("{id:int}/baixar")]
    public async Task<ActionResult<PatrimonioItemDto>> Baixar(int id)
    {
        var item = await _patrimonioItemService.BaixarAsync(id);
        return Ok(item);
    }

    [HttpGet("{id:int}/anexos")]
    public async Task<ActionResult<List<AnexoDto>>> ListarAnexos(int id)
    {
        var anexos = await _patrimonioItemService.ListarAnexosAsync(id);
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

        var anexo = await _patrimonioItemService.AdicionarAnexoAsync(id, new AdicionarAnexoDto
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
        var arquivo = await _patrimonioItemService.ObterAnexoAsync(id, anexoId);
        return File(arquivo.Conteudo, arquivo.TipoConteudo, arquivo.NomeArquivo);
    }

    [HttpDelete("{id:int}/anexos/{anexoId:int}")]
    public async Task<IActionResult> ExcluirAnexo(int id, int anexoId)
    {
        await _patrimonioItemService.ExcluirAnexoAsync(id, anexoId);
        return NoContent();
    }
}

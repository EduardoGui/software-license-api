using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/faturas-plano-saude")]
[Authorize(Roles = Roles.Administrador)]
public class FaturasOperadoraSaudeController : ControllerBase
{
    private readonly IFaturaOperadoraSaudeService _faturaService;

    public FaturasOperadoraSaudeController(IFaturaOperadoraSaudeService faturaService)
    {
        _faturaService = faturaService;
    }

    [HttpGet]
    public async Task<ActionResult<List<FaturaOperadoraSaudeDto>>> GetAll([FromQuery] FaturaOperadoraSaudeFiltroDto filtro)
    {
        var faturas = await _faturaService.GetAllAsync(filtro);
        return Ok(faturas);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<FaturaOperadoraSaudeDto>> GetById(int id)
    {
        var fatura = await _faturaService.GetByIdAsync(id);
        return Ok(fatura);
    }

    [HttpPost]
    public async Task<ActionResult<FaturaOperadoraSaudeDto>> Create(CreateFaturaOperadoraSaudeDto dto)
    {
        var fatura = await _faturaService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = fatura.Id }, fatura);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<FaturaOperadoraSaudeDto>> Update(int id, UpdateFaturaOperadoraSaudeDto dto)
    {
        var fatura = await _faturaService.UpdateAsync(id, dto);
        return Ok(fatura);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _faturaService.DeleteAsync(id);
        return NoContent();
    }

    [HttpGet("{id:int}/anexos")]
    public async Task<ActionResult<List<AnexoDto>>> ListarAnexos(int id)
    {
        var anexos = await _faturaService.ListarAnexosAsync(id);
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

        var anexo = await _faturaService.AdicionarAnexoAsync(id, new AdicionarAnexoDto
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
        var arquivo = await _faturaService.ObterAnexoAsync(id, anexoId);
        return File(arquivo.Conteudo, arquivo.TipoConteudo, arquivo.NomeArquivo);
    }

    [HttpDelete("{id:int}/anexos/{anexoId:int}")]
    public async Task<IActionResult> ExcluirAnexo(int id, int anexoId)
    {
        await _faturaService.ExcluirAnexoAsync(id, anexoId);
        return NoContent();
    }
}

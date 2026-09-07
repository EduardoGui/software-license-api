using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/despesas-avulsas")]
[Authorize(Roles = Roles.Administrador)]
public class DespesasAvulsasController : ControllerBase
{
    private readonly IDespesaAvulsaService _despesaAvulsaService;

    public DespesasAvulsasController(IDespesaAvulsaService despesaAvulsaService)
    {
        _despesaAvulsaService = despesaAvulsaService;
    }

    [HttpGet]
    public async Task<ActionResult<List<DespesaAvulsaDto>>> GetAll([FromQuery] DespesaAvulsaFiltroDto filtro)
    {
        var despesas = await _despesaAvulsaService.GetAllAsync(filtro);
        return Ok(despesas);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DespesaAvulsaDto>> GetById(int id)
    {
        var despesa = await _despesaAvulsaService.GetByIdAsync(id);
        return Ok(despesa);
    }

    [HttpPost]
    public async Task<ActionResult<DespesaAvulsaDto>> Create(CreateDespesaAvulsaDto dto)
    {
        var despesa = await _despesaAvulsaService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = despesa.Id }, despesa);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<DespesaAvulsaDto>> Update(int id, UpdateDespesaAvulsaDto dto)
    {
        var despesa = await _despesaAvulsaService.UpdateAsync(id, dto);
        return Ok(despesa);
    }

    [HttpGet("{id:int}/anexos")]
    public async Task<ActionResult<List<AnexoDto>>> ListarAnexos(int id)
    {
        var anexos = await _despesaAvulsaService.ListarAnexosAsync(id);
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

        var anexo = await _despesaAvulsaService.AdicionarAnexoAsync(id, new AdicionarAnexoDto
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
        var arquivo = await _despesaAvulsaService.ObterAnexoAsync(id, anexoId);
        return File(arquivo.Conteudo, arquivo.TipoConteudo, arquivo.NomeArquivo);
    }

    [HttpDelete("{id:int}/anexos/{anexoId:int}")]
    public async Task<IActionResult> ExcluirAnexo(int id, int anexoId)
    {
        await _despesaAvulsaService.ExcluirAnexoAsync(id, anexoId);
        return NoContent();
    }
}

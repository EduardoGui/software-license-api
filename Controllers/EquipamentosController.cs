using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/equipamentos")]
[Authorize(Roles = Roles.Administrador)]
public class EquipamentosController : ControllerBase
{
    private readonly IEquipamentoService _equipamentoService;
    private readonly IRelatorioMensalLocacaoService _relatorioMensalLocacaoService;

    public EquipamentosController(IEquipamentoService equipamentoService, IRelatorioMensalLocacaoService relatorioMensalLocacaoService)
    {
        _equipamentoService = equipamentoService;
        _relatorioMensalLocacaoService = relatorioMensalLocacaoService;
    }

    [HttpGet]
    public async Task<ActionResult<List<EquipamentoDto>>> GetAll([FromQuery] EquipamentoFiltroDto filtro)
    {
        var equipamentos = await _equipamentoService.GetAllAsync(filtro);
        return Ok(equipamentos);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EquipamentoDto>> GetById(int id)
    {
        var equipamento = await _equipamentoService.GetByIdAsync(id);
        return Ok(equipamento);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<EquipamentoDto>> Update(int id, UpdateEquipamentoDto dto)
    {
        var equipamento = await _equipamentoService.UpdateAsync(id, dto);
        return Ok(equipamento);
    }

    [HttpPatch("{id:int}/baixar")]
    public async Task<ActionResult<EquipamentoDto>> Baixar(int id)
    {
        var equipamento = await _equipamentoService.BaixarAsync(id);
        return Ok(equipamento);
    }

    [HttpGet("inventario")]
    public async Task<ActionResult<InventarioDto>> Inventario()
    {
        var inventario = await _equipamentoService.GetInventarioAsync();
        return Ok(inventario);
    }

    [HttpGet("relatorio-mensal")]
    public async Task<IActionResult> RelatorioMensal([FromQuery] RelatorioMensalLocacaoFiltroDto filtro, [FromQuery] string? formato)
    {
        var relatorio = await _relatorioMensalLocacaoService.GerarAsync(filtro);

        if (string.Equals(formato, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            var arquivo = _relatorioMensalLocacaoService.GerarExcel(relatorio);
            var nomeArquivo = $"relatorio-locacao-{filtro.Ano:D4}-{filtro.Mes:D2}.xlsx";
            return File(arquivo, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nomeArquivo);
        }

        return Ok(relatorio);
    }

    [HttpGet("{id:int}/anexos")]
    public async Task<ActionResult<List<AnexoDto>>> ListarAnexos(int id)
    {
        var anexos = await _equipamentoService.ListarAnexosAsync(id);
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

        var anexo = await _equipamentoService.AdicionarAnexoAsync(id, new AdicionarAnexoDto
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
        var arquivo = await _equipamentoService.ObterAnexoAsync(id, anexoId);
        return File(arquivo.Conteudo, arquivo.TipoConteudo, arquivo.NomeArquivo);
    }

    [HttpDelete("{id:int}/anexos/{anexoId:int}")]
    public async Task<IActionResult> ExcluirAnexo(int id, int anexoId)
    {
        await _equipamentoService.ExcluirAnexoAsync(id, anexoId);
        return NoContent();
    }
}

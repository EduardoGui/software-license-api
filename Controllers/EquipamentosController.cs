using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/equipamentos")]
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
}

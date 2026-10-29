using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/plano-saude-custos")]
[Authorize(Roles = $"{Roles.Administrador},{Roles.Administrativo}")]
public class PlanoSaudeCustosController : ControllerBase
{
    private readonly IPlanoSaudeCustoService _planoSaudeCustoService;
    private readonly IRelatorioMensalPlanoSaudeService _relatorioMensalPlanoSaudeService;
    private readonly IPlanoSaudeImportacaoService _importacaoService;

    public PlanoSaudeCustosController(
        IPlanoSaudeCustoService planoSaudeCustoService, IRelatorioMensalPlanoSaudeService relatorioMensalPlanoSaudeService,
        IPlanoSaudeImportacaoService importacaoService)
    {
        _importacaoService = importacaoService;
        _planoSaudeCustoService = planoSaudeCustoService;
        _relatorioMensalPlanoSaudeService = relatorioMensalPlanoSaudeService;
    }

    [HttpGet("mes")]
    public async Task<ActionResult<PlanoSaudeMesDto>> GetMes([FromQuery] PlanoSaudeMesFiltroDto filtro)
    {
        var mes = await _planoSaudeCustoService.GetMesAsync(filtro);
        return Ok(mes);
    }

    // Importa o demonstrativo analítico da operadora (xlsx). A prévia não grava nada; a confirmação reenvia o arquivo
    // com a assinatura da prévia e só grava se o resultado continuar o mesmo.
    [HttpPost("importar-fatura/previa")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ImportacaoPlanoSaudePreviaDto>> PreviaImportacao(IFormFile arquivo, [FromForm] int ano, [FromForm] int mes)
    {
        var erro = ValidarArquivo(arquivo);
        if (erro is not null)
        {
            return BadRequest(new { message = erro });
        }

        using var stream = new MemoryStream();
        await arquivo.CopyToAsync(stream);
        stream.Position = 0;
        return Ok(await _importacaoService.PreviaAsync(stream, ano, mes));
    }

    [HttpPost("importar-fatura/confirmar")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ImportacaoPlanoSaudeResultadoDto>> ConfirmarImportacao(
        IFormFile arquivo, [FromForm] int ano, [FromForm] int mes, [FromForm] string? assinatura)
    {
        var erro = ValidarArquivo(arquivo);
        if (erro is not null)
        {
            return BadRequest(new { message = erro });
        }

        using var stream = new MemoryStream();
        await arquivo.CopyToAsync(stream);
        stream.Position = 0;
        return Ok(await _importacaoService.ConfirmarAsync(stream, ano, mes, assinatura));
    }

    private static string? ValidarArquivo(IFormFile? arquivo)
    {
        if (arquivo is null || arquivo.Length == 0)
        {
            return "Nenhum arquivo enviado.";
        }

        return arquivo.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ? null : "Envie a planilha no formato .xlsx.";
    }

    [HttpPost("mes")]
    public async Task<ActionResult<PlanoSaudeMesDto>> SalvarMes(SalvarPlanoSaudeMesDto dto)
    {
        var mes = await _planoSaudeCustoService.SalvarMesAsync(dto);
        return Ok(mes);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Remover(int id)
    {
        await _planoSaudeCustoService.RemoverAsync(id);
        return NoContent();
    }

    [HttpGet("ultimo-mes")]
    public async Task<ActionResult<UltimoMesPlanoSaudeDto>> ObterUltimoMes()
    {
        var ultimoMes = await _planoSaudeCustoService.ObterUltimoMesLancadoAsync();
        return Ok(ultimoMes);
    }

    [HttpGet("relatorio-mensal")]
    public async Task<IActionResult> RelatorioMensal([FromQuery] RelatorioMensalPlanoSaudeFiltroDto filtro, [FromQuery] string? formato)
    {
        var relatorio = await _relatorioMensalPlanoSaudeService.GerarAsync(filtro);

        if (string.Equals(formato, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            var arquivo = _relatorioMensalPlanoSaudeService.GerarExcel(relatorio);
            var nomeArquivo = $"relatorio-plano-saude-{filtro.Ano:D4}-{filtro.Mes:D2}.xlsx";
            return File(arquivo, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nomeArquivo);
        }

        return Ok(relatorio);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/licencas")]
[Authorize(Roles = Roles.Administrador)]
public class LicencasController : ControllerBase
{
    private readonly ILicencaService _licencaService;
    private readonly IRelatorioMensalCustoLicencasService _relatorioMensalCustoLicencasService;

    public LicencasController(ILicencaService licencaService, IRelatorioMensalCustoLicencasService relatorioMensalCustoLicencasService)
    {
        _licencaService = licencaService;
        _relatorioMensalCustoLicencasService = relatorioMensalCustoLicencasService;
    }

    [HttpGet]
    public async Task<ActionResult<List<LicencaDto>>> GetAll([FromQuery] LicencaFiltroDto filtro)
    {
        var licencas = await _licencaService.GetAllAsync(filtro);
        return Ok(licencas);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LicencaDto>> GetById(int id)
    {
        var licenca = await _licencaService.GetByIdAsync(id);
        return Ok(licenca);
    }

    [HttpPost]
    public async Task<ActionResult<LicencaDto>> Create(CreateLicencaDto dto)
    {
        var licenca = await _licencaService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = licenca.Id }, licenca);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<LicencaDto>> Update(int id, UpdateLicencaDto dto)
    {
        var licenca = await _licencaService.UpdateAsync(id, dto);
        return Ok(licenca);
    }

    [HttpPatch("{id:int}/desativar")]
    public async Task<ActionResult<LicencaDto>> Desativar(int id)
    {
        var licenca = await _licencaService.DesativarAsync(id);
        return Ok(licenca);
    }

    [HttpGet("{id:int}/valores")]
    public async Task<ActionResult<List<LicencaValorDto>>> ListarValores(int id)
    {
        var valores = await _licencaService.ListarValoresAsync(id);
        return Ok(valores);
    }

    [HttpPost("{id:int}/valores")]
    public async Task<ActionResult<LicencaDto>> AdicionarValor(int id, CreateLicencaValorDto dto)
    {
        var licenca = await _licencaService.AdicionarValorAsync(id, dto);
        return Ok(licenca);
    }

    [HttpGet("relatorio-mensal")]
    public async Task<IActionResult> RelatorioMensal([FromQuery] RelatorioMensalCustoLicencasFiltroDto filtro, [FromQuery] string? formato)
    {
        var relatorio = await _relatorioMensalCustoLicencasService.GerarAsync(filtro);

        if (string.Equals(formato, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            var arquivo = _relatorioMensalCustoLicencasService.GerarExcel(relatorio);
            var nomeArquivo = $"relatorio-custo-licencas-{filtro.Ano:D4}-{filtro.Mes:D2}.xlsx";
            return File(arquivo, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nomeArquivo);
        }

        return Ok(relatorio);
    }
}

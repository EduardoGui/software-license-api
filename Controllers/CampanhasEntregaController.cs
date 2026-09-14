using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/campanhas-entrega")]
[Authorize(Roles = Roles.Administrador)]
public class CampanhasEntregaController : ControllerBase
{
    private readonly ICampanhaEntregaService _campanhaEntregaService;

    public CampanhasEntregaController(ICampanhaEntregaService campanhaEntregaService)
    {
        _campanhaEntregaService = campanhaEntregaService;
    }

    [HttpGet]
    public async Task<ActionResult<List<CampanhaEntregaDto>>> GetAll([FromQuery] CampanhaEntregaFiltroDto filtro)
    {
        return Ok(await _campanhaEntregaService.GetAllAsync(filtro));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CampanhaEntregaDto>> GetById(int id)
    {
        return Ok(await _campanhaEntregaService.GetByIdAsync(id));
    }

    [HttpPost]
    public async Task<ActionResult<CampanhaEntregaDto>> Create(CreateCampanhaEntregaDto dto)
    {
        var campanha = await _campanhaEntregaService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = campanha.Id }, campanha);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CampanhaEntregaDto>> Update(int id, UpdateCampanhaEntregaDto dto)
    {
        return Ok(await _campanhaEntregaService.UpdateAsync(id, dto));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _campanhaEntregaService.DeleteAsync(id);
        return NoContent();
    }

    [HttpPatch("{id:int}/cancelar")]
    public async Task<ActionResult<CampanhaEntregaDto>> Cancelar(int id)
    {
        return Ok(await _campanhaEntregaService.CancelarAsync(id));
    }

    [HttpPatch("{id:int}/encerrar")]
    public async Task<ActionResult<CampanhaEntregaDto>> Encerrar(int id)
    {
        return Ok(await _campanhaEntregaService.EncerrarAsync(id));
    }

    [HttpGet("{id:int}/resumo")]
    public async Task<ActionResult<CampanhaEntregaResumoDto>> ObterResumo(int id)
    {
        return Ok(await _campanhaEntregaService.ObterResumoAsync(id));
    }

    [HttpGet("{id:int}/colaboradores-disponiveis")]
    public async Task<ActionResult<List<ColaboradorDisponivelDto>>> ListarColaboradoresDisponiveis(int id, [FromQuery] ColaboradorDisponivelFiltroDto filtro)
    {
        return Ok(await _campanhaEntregaService.ListarColaboradoresDisponiveisAsync(id, filtro));
    }

    [HttpGet("{id:int}/entregas")]
    public async Task<ActionResult<List<EntregaDto>>> ListarEntregas(int id, [FromQuery] EntregaFiltroDto filtro)
    {
        return Ok(await _campanhaEntregaService.ListarEntregasAsync(id, filtro));
    }

    [HttpGet("{id:int}/entregas/{entregaId:int}")]
    public async Task<ActionResult<EntregaDto>> ObterEntrega(int id, int entregaId)
    {
        return Ok(await _campanhaEntregaService.ObterEntregaAsync(id, entregaId));
    }

    [HttpPost("{id:int}/entregas")]
    public async Task<ActionResult<EntregaDto>> AdicionarEntrega(int id, CreateEntregaDto dto)
    {
        var entrega = await _campanhaEntregaService.AdicionarEntregaAsync(id, dto);
        return CreatedAtAction(nameof(ObterEntrega), new { id, entregaId = entrega.Id }, entrega);
    }

    [HttpPost("{id:int}/entregas/lote")]
    public async Task<ActionResult<List<EntregaDto>>> AdicionarEntregasLote(int id, CreateEntregaLoteDto dto)
    {
        return Ok(await _campanhaEntregaService.AdicionarEntregasLoteAsync(id, dto));
    }

    [HttpPut("{id:int}/entregas/{entregaId:int}/itens")]
    public async Task<ActionResult<EntregaDto>> AtualizarItensEntrega(int id, int entregaId, UpdateEntregaItensDto dto)
    {
        return Ok(await _campanhaEntregaService.AtualizarItensEntregaAsync(id, entregaId, dto));
    }

    [HttpPatch("{id:int}/entregas/{entregaId:int}/registrar-entrega-fisica")]
    public async Task<ActionResult<EntregaDto>> RegistrarEntregaFisica(int id, int entregaId, RegistrarEntregaFisicaDto dto)
    {
        return Ok(await _campanhaEntregaService.RegistrarEntregaFisicaAsync(id, entregaId, dto));
    }

    [HttpPatch("{id:int}/entregas/{entregaId:int}/cancelar")]
    public async Task<ActionResult<EntregaDto>> CancelarEntrega(int id, int entregaId)
    {
        return Ok(await _campanhaEntregaService.CancelarEntregaAsync(id, entregaId));
    }
}

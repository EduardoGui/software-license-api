using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/notas-fiscais-entrada")]
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
}

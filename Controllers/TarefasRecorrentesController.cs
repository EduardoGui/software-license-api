using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/tarefas-recorrentes")]
[Authorize(Roles = Roles.Administrador)]
public class TarefasRecorrentesController : ControllerBase
{
    private readonly ITarefaRecorrenteService _tarefaRecorrenteService;

    public TarefasRecorrentesController(ITarefaRecorrenteService tarefaRecorrenteService)
    {
        _tarefaRecorrenteService = tarefaRecorrenteService;
    }

    [HttpGet]
    public async Task<ActionResult<List<TarefaRecorrenteDto>>> GetAll([FromQuery] TarefaRecorrenteFiltroDto filtro)
    {
        var tarefas = await _tarefaRecorrenteService.GetAllAsync(filtro);
        return Ok(tarefas);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TarefaRecorrenteDto>> GetById(int id)
    {
        var tarefa = await _tarefaRecorrenteService.GetByIdAsync(id);
        return Ok(tarefa);
    }

    [HttpPost]
    public async Task<ActionResult<TarefaRecorrenteDto>> Create(CreateTarefaRecorrenteDto dto)
    {
        var tarefa = await _tarefaRecorrenteService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = tarefa.Id }, tarefa);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TarefaRecorrenteDto>> Update(int id, UpdateTarefaRecorrenteDto dto)
    {
        var tarefa = await _tarefaRecorrenteService.UpdateAsync(id, dto);
        return Ok(tarefa);
    }
}

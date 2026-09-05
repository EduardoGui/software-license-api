using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/agenda")]
[Authorize(Roles = Roles.Administrador)]
public class AgendaController : ControllerBase
{
    private readonly ITarefaOcorrenciaService _tarefaOcorrenciaService;

    public AgendaController(ITarefaOcorrenciaService tarefaOcorrenciaService)
    {
        _tarefaOcorrenciaService = tarefaOcorrenciaService;
    }

    [HttpGet]
    public async Task<ActionResult<List<TarefaOcorrenciaDto>>> GetAgenda()
    {
        var agenda = await _tarefaOcorrenciaService.ObterAgendaAsync();
        return Ok(agenda);
    }

    [HttpPatch("{ocorrenciaId:int}/concluir")]
    public async Task<ActionResult<TarefaOcorrenciaDto>> Concluir(int ocorrenciaId)
    {
        var ocorrencia = await _tarefaOcorrenciaService.ConcluirAsync(ocorrenciaId);
        return Ok(ocorrencia);
    }

    [HttpPatch("{ocorrenciaId:int}/adiar")]
    public async Task<ActionResult<TarefaOcorrenciaDto>> Adiar(int ocorrenciaId, AdiarTarefaOcorrenciaDto dto)
    {
        var ocorrencia = await _tarefaOcorrenciaService.AdiarAsync(ocorrenciaId, dto);
        return Ok(ocorrencia);
    }

    [HttpPost("tarefa-unica")]
    public async Task<ActionResult<TarefaOcorrenciaDto>> CriarTarefaUnica(CreateTarefaUnicaDto dto)
    {
        var ocorrencia = await _tarefaOcorrenciaService.CriarTarefaUnicaAsync(dto);
        return CreatedAtAction(nameof(GetAgenda), ocorrencia);
    }
}

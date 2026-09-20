using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Extensions;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/programacoes-ferias")]
[Authorize(Roles = Roles.Administrador)]
public class ProgramacoesFeriasController : ControllerBase
{
    private readonly IProgramacaoFeriasService _programacaoFeriasService;

    public ProgramacoesFeriasController(IProgramacaoFeriasService programacaoFeriasService)
    {
        _programacaoFeriasService = programacaoFeriasService;
    }

    [HttpGet("pendentes-aprovacao")]
    public async Task<ActionResult<List<ProgramacaoFeriasDto>>> GetPendentesAprovacao()
    {
        var pendentes = await _programacaoFeriasService.GetPendentesAprovacaoAsync();
        return Ok(pendentes);
    }

    [HttpGet]
    [Route("/api/periodos-ferias/{periodoFeriasId:int}/programacoes")]
    public async Task<ActionResult<List<ProgramacaoFeriasDto>>> GetByPeriodo(int periodoFeriasId)
    {
        var programacoes = await _programacaoFeriasService.GetByPeriodoAsync(periodoFeriasId);
        return Ok(programacoes);
    }

    [HttpPost]
    [Route("/api/periodos-ferias/{periodoFeriasId:int}/programacoes")]
    public async Task<ActionResult<ProgramacaoFeriasDto>> Create(int periodoFeriasId, CreateProgramacaoFeriasDto dto)
    {
        var programacao = await _programacaoFeriasService.CreateAsync(periodoFeriasId, dto, User.ObterUsuarioId());
        return CreatedAtAction(nameof(GetById), new { id = programacao.Id }, programacao);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProgramacaoFeriasDto>> GetById(int id)
    {
        var programacao = await _programacaoFeriasService.GetByIdAsync(id);
        return Ok(programacao);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProgramacaoFeriasDto>> Update(int id, CreateProgramacaoFeriasDto dto)
    {
        var programacao = await _programacaoFeriasService.UpdateAsync(id, dto, User.ObterUsuarioId());
        return Ok(programacao);
    }

    [HttpPatch("{id:int}/solicitar")]
    public async Task<ActionResult<ProgramacaoFeriasDto>> Solicitar(int id)
    {
        var programacao = await _programacaoFeriasService.SolicitarAsync(id, User.ObterUsuarioId());
        return Ok(programacao);
    }

    [HttpPatch("{id:int}/aprovar")]
    public async Task<ActionResult<ProgramacaoFeriasDto>> Aprovar(int id)
    {
        var programacao = await _programacaoFeriasService.AprovarAsync(id, User.ObterUsuarioId());
        return Ok(programacao);
    }

    [HttpPatch("{id:int}/reprovar")]
    public async Task<ActionResult<ProgramacaoFeriasDto>> Reprovar(int id, DecisaoProgramacaoFeriasDto dto)
    {
        var programacao = await _programacaoFeriasService.ReprovarAsync(id, dto, User.ObterUsuarioId());
        return Ok(programacao);
    }

    [HttpPatch("{id:int}/devolver")]
    public async Task<ActionResult<ProgramacaoFeriasDto>> Devolver(int id, DecisaoProgramacaoFeriasDto dto)
    {
        var programacao = await _programacaoFeriasService.DevolverAsync(id, dto, User.ObterUsuarioId());
        return Ok(programacao);
    }

    [HttpPatch("{id:int}/cancelar")]
    public async Task<ActionResult<ProgramacaoFeriasDto>> Cancelar(int id)
    {
        var programacao = await _programacaoFeriasService.CancelarAsync(id, User.ObterUsuarioId());
        return Ok(programacao);
    }
}

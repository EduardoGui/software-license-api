using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Extensions;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/programacoes-ferias")]
[Authorize(Roles = $"{Roles.Administrador},{Roles.Colaborador}")]
public class ProgramacoesFeriasController : ControllerBase
{
    private readonly IProgramacaoFeriasService _programacaoFeriasService;

    public ProgramacoesFeriasController(IProgramacaoFeriasService programacaoFeriasService)
    {
        _programacaoFeriasService = programacaoFeriasService;
    }

    [HttpGet("pendentes-aprovacao")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<List<ProgramacaoFeriasDto>>> GetPendentesAprovacao()
    {
        var pendentes = await _programacaoFeriasService.GetPendentesAprovacaoAsync();
        return Ok(pendentes);
    }

    // Usada pelo mobile - o colaborador PJ vê só as próprias programações, de qualquer
    // status (Rascunho/Solicitada/Aprovada/Cancelada/Reprovada).
    [HttpGet]
    [Route("/api/usuarios/{usuarioId:int}/programacoes-ferias")]
    public async Task<ActionResult<List<ProgramacaoFeriasDto>>> GetByUsuario(int usuarioId)
    {
        if (!User.IsInRole(Roles.Administrador) && !User.TemUsuarioId(usuarioId))
        {
            return Forbid();
        }

        var programacoes = await _programacaoFeriasService.GetByUsuarioAsync(usuarioId);
        return Ok(programacoes);
    }

    [HttpGet]
    [Route("/api/periodos-ferias/{periodoFeriasId:int}/programacoes")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<List<ProgramacaoFeriasDto>>> GetByPeriodo(int periodoFeriasId)
    {
        var programacoes = await _programacaoFeriasService.GetByPeriodoAsync(periodoFeriasId);
        return Ok(programacoes);
    }

    [HttpPost]
    [Route("/api/periodos-ferias/{periodoFeriasId:int}/programacoes")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<ProgramacaoFeriasDto>> Create(int periodoFeriasId, CreateProgramacaoFeriasDto dto)
    {
        var programacao = await _programacaoFeriasService.CreateAsync(periodoFeriasId, dto, User.ObterUsuarioId());
        return CreatedAtAction(nameof(GetById), new { id = programacao.Id }, programacao);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<ProgramacaoFeriasDto>> GetById(int id)
    {
        var programacao = await _programacaoFeriasService.GetByIdAsync(id);
        return Ok(programacao);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<ProgramacaoFeriasDto>> Update(int id, CreateProgramacaoFeriasDto dto)
    {
        var programacao = await _programacaoFeriasService.UpdateAsync(id, dto, User.ObterUsuarioId());
        return Ok(programacao);
    }

    [HttpPatch("{id:int}/solicitar")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<ProgramacaoFeriasDto>> Solicitar(int id)
    {
        var programacao = await _programacaoFeriasService.SolicitarAsync(id, User.ObterUsuarioId());
        return Ok(programacao);
    }

    [HttpPatch("{id:int}/aprovar")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<ProgramacaoFeriasDto>> Aprovar(int id)
    {
        var programacao = await _programacaoFeriasService.AprovarAsync(id, User.ObterUsuarioId());
        return Ok(programacao);
    }

    [HttpPatch("{id:int}/reprovar")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<ProgramacaoFeriasDto>> Reprovar(int id, DecisaoProgramacaoFeriasDto dto)
    {
        var programacao = await _programacaoFeriasService.ReprovarAsync(id, dto, User.ObterUsuarioId());
        return Ok(programacao);
    }

    [HttpPatch("{id:int}/devolver")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<ProgramacaoFeriasDto>> Devolver(int id, DecisaoProgramacaoFeriasDto dto)
    {
        var programacao = await _programacaoFeriasService.DevolverAsync(id, dto, User.ObterUsuarioId());
        return Ok(programacao);
    }

    [HttpPatch("{id:int}/cancelar")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<ProgramacaoFeriasDto>> Cancelar(int id)
    {
        var programacao = await _programacaoFeriasService.CancelarAsync(id, User.ObterUsuarioId());
        return Ok(programacao);
    }
}

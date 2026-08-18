using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Extensions;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/equipamento-alocacoes")]
public class EquipamentoAlocacoesController : ControllerBase
{
    private readonly IEquipamentoAlocacaoService _equipamentoAlocacaoService;

    public EquipamentoAlocacoesController(IEquipamentoAlocacaoService equipamentoAlocacaoService)
    {
        _equipamentoAlocacaoService = equipamentoAlocacaoService;
    }

    [HttpGet]
    [Authorize(Roles = $"{Roles.Administrador},{Roles.Colaborador}")]
    public async Task<ActionResult<PaginaDto<EquipamentoAlocacaoDto>>> GetAll([FromQuery] EquipamentoAlocacaoFiltroDto filtro)
    {
        if (!User.IsInRole(Roles.Administrador) && (filtro.UsuarioId is null || !User.TemUsuarioId(filtro.UsuarioId.Value)))
        {
            return Forbid();
        }

        var pagina = await _equipamentoAlocacaoService.GetAllAsync(filtro);
        return Ok(pagina);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<EquipamentoAlocacaoDto>> Create(CreateEquipamentoAlocacaoDto dto)
    {
        var alocacao = await _equipamentoAlocacaoService.CreateAsync(dto);
        return Ok(alocacao);
    }

    [HttpPatch("{id:int}/encerrar")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<EquipamentoAlocacaoDto>> Encerrar(int id, EncerrarEquipamentoAlocacaoDto dto)
    {
        var alocacao = await _equipamentoAlocacaoService.EncerrarAsync(id, dto);
        return Ok(alocacao);
    }

    [HttpPatch("{id:int}/editar-encerramento")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<EquipamentoAlocacaoDto>> EditarEncerramento(int id, EditarEquipamentoAlocacaoEncerradaDto dto)
    {
        var alocacao = await _equipamentoAlocacaoService.EditarEncerradaAsync(id, dto);
        return Ok(alocacao);
    }
}

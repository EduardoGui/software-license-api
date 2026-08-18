using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Extensions;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/movimentacoes")]
public class MovimentacoesController : ControllerBase
{
    private readonly IMovimentacaoService _movimentacaoService;

    public MovimentacoesController(IMovimentacaoService movimentacaoService)
    {
        _movimentacaoService = movimentacaoService;
    }

    [HttpGet]
    [Authorize(Roles = $"{Roles.Administrador},{Roles.Colaborador}")]
    public async Task<ActionResult<PaginaDto<MovimentacaoDto>>> GetAll([FromQuery] MovimentacaoFiltroDto filtro)
    {
        if (!User.IsInRole(Roles.Administrador) && (filtro.UsuarioId is null || !User.TemUsuarioId(filtro.UsuarioId.Value)))
        {
            return Forbid();
        }

        var movimentacoes = await _movimentacaoService.GetAllAsync(filtro);
        return Ok(movimentacoes);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<MovimentacaoDto>> Create(CreateMovimentacaoDto dto)
    {
        var movimentacao = await _movimentacaoService.CreateAsync(dto);
        return Ok(movimentacao);
    }

    [HttpPatch("{id:int}/encerrar")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<MovimentacaoDto>> Encerrar(int id, EncerrarMovimentacaoDto dto)
    {
        var movimentacao = await _movimentacaoService.EncerrarAsync(id, dto);
        return Ok(movimentacao);
    }

    [HttpPatch("{id:int}/editar-encerramento")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<MovimentacaoDto>> EditarEncerramento(int id, EditarMovimentacaoEncerradaDto dto)
    {
        var movimentacao = await _movimentacaoService.EditarEncerradaAsync(id, dto);
        return Ok(movimentacao);
    }
}

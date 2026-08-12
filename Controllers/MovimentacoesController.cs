using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
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
    public async Task<ActionResult<PaginaDto<MovimentacaoDto>>> GetAll([FromQuery] MovimentacaoFiltroDto filtro)
    {
        var movimentacoes = await _movimentacaoService.GetAllAsync(filtro);
        return Ok(movimentacoes);
    }

    [HttpPost]
    public async Task<ActionResult<MovimentacaoDto>> Create(CreateMovimentacaoDto dto)
    {
        var movimentacao = await _movimentacaoService.CreateAsync(dto);
        return Ok(movimentacao);
    }

    [HttpPatch("{id:int}/encerrar")]
    public async Task<ActionResult<MovimentacaoDto>> Encerrar(int id, EncerrarMovimentacaoDto dto)
    {
        var movimentacao = await _movimentacaoService.EncerrarAsync(id, dto);
        return Ok(movimentacao);
    }
}

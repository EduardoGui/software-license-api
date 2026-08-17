using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
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
    public async Task<ActionResult<PaginaDto<EquipamentoAlocacaoDto>>> GetAll([FromQuery] EquipamentoAlocacaoFiltroDto filtro)
    {
        var pagina = await _equipamentoAlocacaoService.GetAllAsync(filtro);
        return Ok(pagina);
    }

    [HttpPost]
    public async Task<ActionResult<EquipamentoAlocacaoDto>> Create(CreateEquipamentoAlocacaoDto dto)
    {
        var alocacao = await _equipamentoAlocacaoService.CreateAsync(dto);
        return Ok(alocacao);
    }

    [HttpPatch("{id:int}/encerrar")]
    public async Task<ActionResult<EquipamentoAlocacaoDto>> Encerrar(int id, EncerrarEquipamentoAlocacaoDto dto)
    {
        var alocacao = await _equipamentoAlocacaoService.EncerrarAsync(id, dto);
        return Ok(alocacao);
    }
}

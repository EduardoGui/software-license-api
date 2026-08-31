using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/fornecedores")]
[Authorize(Roles = Roles.Administrador)]
public class FornecedoresController : ControllerBase
{
    private readonly IFornecedorService _fornecedorService;

    public FornecedoresController(IFornecedorService fornecedorService)
    {
        _fornecedorService = fornecedorService;
    }

    [HttpGet]
    public async Task<ActionResult<List<FornecedorDto>>> GetAll([FromQuery] FornecedorFiltroDto filtro)
    {
        var fornecedores = await _fornecedorService.GetAllAsync(filtro);
        return Ok(fornecedores);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<FornecedorDto>> GetById(int id)
    {
        var fornecedor = await _fornecedorService.GetByIdAsync(id);
        return Ok(fornecedor);
    }

    [HttpPost]
    public async Task<ActionResult<FornecedorDto>> Create(CreateFornecedorDto dto)
    {
        var fornecedor = await _fornecedorService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = fornecedor.Id }, fornecedor);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<FornecedorDto>> Update(int id, UpdateFornecedorDto dto)
    {
        var fornecedor = await _fornecedorService.UpdateAsync(id, dto);
        return Ok(fornecedor);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/empresas-pj")]
[Authorize(Roles = Roles.Administrador)]
public class EmpresasPjController : ControllerBase
{
    private readonly IEmpresaPjService _empresaPjService;

    public EmpresasPjController(IEmpresaPjService empresaPjService)
    {
        _empresaPjService = empresaPjService;
    }

    [HttpGet]
    public async Task<ActionResult<List<EmpresaPjDto>>> GetAll([FromQuery] EmpresaPjFiltroDto filtro)
    {
        var empresas = await _empresaPjService.GetAllAsync(filtro);
        return Ok(empresas);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmpresaPjDto>> GetById(int id)
    {
        var empresa = await _empresaPjService.GetByIdAsync(id);
        return Ok(empresa);
    }

    [HttpPost]
    public async Task<ActionResult<EmpresaPjDto>> Create(CreateEmpresaPjDto dto)
    {
        var empresa = await _empresaPjService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = empresa.Id }, empresa);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<EmpresaPjDto>> Update(int id, UpdateEmpresaPjDto dto)
    {
        var empresa = await _empresaPjService.UpdateAsync(id, dto);
        return Ok(empresa);
    }
}

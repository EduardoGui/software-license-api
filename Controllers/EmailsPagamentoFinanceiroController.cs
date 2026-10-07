using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/emails-pagamento-financeiro")]
[Authorize(Roles = Roles.Administrador)]
public class EmailsPagamentoFinanceiroController : ControllerBase
{
    private readonly IEmailPagamentoFinanceiroService _emailPagamentoFinanceiroService;

    public EmailsPagamentoFinanceiroController(IEmailPagamentoFinanceiroService emailPagamentoFinanceiroService)
    {
        _emailPagamentoFinanceiroService = emailPagamentoFinanceiroService;
    }

    [HttpGet]
    public async Task<ActionResult<List<EmailPagamentoFinanceiroDto>>> GetAll([FromQuery] EmailPagamentoFinanceiroFiltroDto filtro)
    {
        var emails = await _emailPagamentoFinanceiroService.GetAllAsync(filtro);
        return Ok(emails);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmailPagamentoFinanceiroDto>> GetById(int id)
    {
        var email = await _emailPagamentoFinanceiroService.GetByIdAsync(id);
        return Ok(email);
    }

    [HttpPost]
    public async Task<ActionResult<EmailPagamentoFinanceiroDto>> Create(CreateEmailPagamentoFinanceiroDto dto)
    {
        var email = await _emailPagamentoFinanceiroService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = email.Id }, email);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<EmailPagamentoFinanceiroDto>> Update(int id, UpdateEmailPagamentoFinanceiroDto dto)
    {
        var email = await _emailPagamentoFinanceiroService.UpdateAsync(id, dto);
        return Ok(email);
    }
}

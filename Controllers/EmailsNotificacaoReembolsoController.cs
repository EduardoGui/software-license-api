using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/emails-notificacao-reembolso")]
[Authorize(Roles = Roles.Administrador)]
public class EmailsNotificacaoReembolsoController : ControllerBase
{
    private readonly IEmailNotificacaoReembolsoService _emailNotificacaoReembolsoService;

    public EmailsNotificacaoReembolsoController(IEmailNotificacaoReembolsoService emailNotificacaoReembolsoService)
    {
        _emailNotificacaoReembolsoService = emailNotificacaoReembolsoService;
    }

    [HttpGet]
    public async Task<ActionResult<List<EmailNotificacaoReembolsoDto>>> GetAll([FromQuery] EmailNotificacaoReembolsoFiltroDto filtro)
    {
        var emails = await _emailNotificacaoReembolsoService.GetAllAsync(filtro);
        return Ok(emails);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmailNotificacaoReembolsoDto>> GetById(int id)
    {
        var email = await _emailNotificacaoReembolsoService.GetByIdAsync(id);
        return Ok(email);
    }

    [HttpPost]
    public async Task<ActionResult<EmailNotificacaoReembolsoDto>> Create(CreateEmailNotificacaoReembolsoDto dto)
    {
        var email = await _emailNotificacaoReembolsoService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = email.Id }, email);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<EmailNotificacaoReembolsoDto>> Update(int id, UpdateEmailNotificacaoReembolsoDto dto)
    {
        var email = await _emailNotificacaoReembolsoService.UpdateAsync(id, dto);
        return Ok(email);
    }
}

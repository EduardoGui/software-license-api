using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Extensions;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/obrigacoes")]
[Authorize(Roles = $"{Roles.Administrador},{Roles.Administrativo}")]
public class ObrigacoesController : ControllerBase
{
    private readonly IObrigacaoService _obrigacaoService;
    private readonly ISolicitacaoPagamentoService _solicitacaoPagamentoService;

    public ObrigacoesController(IObrigacaoService obrigacaoService, ISolicitacaoPagamentoService solicitacaoPagamentoService)
    {
        _obrigacaoService = obrigacaoService;
        _solicitacaoPagamentoService = solicitacaoPagamentoService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ObrigacaoDto>>> GetAll([FromQuery] ObrigacaoFiltroDto filtro)
    {
        var obrigacoes = await _obrigacaoService.GetAllAsync(filtro);
        return Ok(obrigacoes);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ObrigacaoDto>> GetById(int id)
    {
        var obrigacao = await _obrigacaoService.GetByIdAsync(id);
        return Ok(obrigacao);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ObrigacaoDto>> Update(int id, UpdateObrigacaoDto dto)
    {
        var obrigacao = await _obrigacaoService.UpdateAsync(id, dto);
        return Ok(obrigacao);
    }

    // Ordem de Compra paga em várias vezes: cria mais uma obrigação (parcela) ligada à mesma OC, copiando a NF.
    [HttpPost("{id:int}/parcelas")]
    public async Task<ActionResult<ObrigacaoDto>> AdicionarParcela(int id, AdicionarParcelaObrigacaoDto dto)
    {
        var parcela = await _obrigacaoService.AdicionarParcelaAsync(id, dto);
        return CreatedAtAction(nameof(GetById), new { id = parcela.Id }, parcela);
    }

    [HttpGet("{id:int}/solicitacao-pagamento")]
    public async Task<ActionResult<SolicitacaoPagamentoDto>> SolicitacaoPagamento(int id)
    {
        var solicitacao = await _solicitacaoPagamentoService.GerarAsync(id, User.ObterUsuarioId());
        return Ok(solicitacao);
    }

    [HttpGet("{id:int}/solicitacao-pagamento/eml")]
    public async Task<IActionResult> SolicitacaoPagamentoEml(int id)
    {
        var (arquivo, nome) = await _solicitacaoPagamentoService.GerarEmlAsync(id, User.ObterUsuarioId());
        return File(arquivo, "message/rfc822", nome);
    }

    [HttpPatch("{id:int}/andamento")]
    public async Task<ActionResult<ObrigacaoDto>> AtualizarAndamento(int id, AtualizarAndamentoObrigacaoDto dto)
    {
        var obrigacao = await _obrigacaoService.AtualizarAndamentoAsync(id, dto);
        return Ok(obrigacao);
    }

    [HttpPatch("{id:int}/marcar-enviada-financeiro")]
    public async Task<ActionResult<ObrigacaoDto>> MarcarEnviadaFinanceiro(int id, MarcarEnviadaFinanceiroDto dto)
    {
        var obrigacao = await _obrigacaoService.MarcarEnviadaFinanceiroAsync(id, dto);
        return Ok(obrigacao);
    }

    [HttpPatch("{id:int}/marcar-paga")]
    public async Task<ActionResult<ObrigacaoDto>> MarcarPaga(int id)
    {
        var obrigacao = await _obrigacaoService.MarcarPagaAsync(id);
        return Ok(obrigacao);
    }

    [HttpPatch("{id:int}/desmarcar-paga")]
    public async Task<ActionResult<ObrigacaoDto>> DesmarcarPaga(int id)
    {
        var obrigacao = await _obrigacaoService.DesmarcarPagaAsync(id);
        return Ok(obrigacao);
    }

    [HttpPatch("{id:int}/cancelar")]
    public async Task<ActionResult<ObrigacaoDto>> Cancelar(int id)
    {
        var obrigacao = await _obrigacaoService.CancelarAsync(id);
        return Ok(obrigacao);
    }

    [HttpPatch("{id:int}/reativar")]
    public async Task<ActionResult<ObrigacaoDto>> Reativar(int id)
    {
        var obrigacao = await _obrigacaoService.ReativarAsync(id);
        return Ok(obrigacao);
    }
}

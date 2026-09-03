using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Extensions;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/contratos")]
[Authorize(Roles = Roles.Administrador)]
public class ContratosController : ControllerBase
{
    private readonly IContratoService _contratoService;

    public ContratosController(IContratoService contratoService)
    {
        _contratoService = contratoService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ContratoDto>>> GetAll([FromQuery] ContratoFiltroDto filtro)
    {
        var contratos = await _contratoService.GetAllAsync(filtro);
        return Ok(contratos);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ContratoDetalheDto>> GetById(int id)
    {
        var contrato = await _contratoService.GetByIdAsync(id);
        return Ok(contrato);
    }

    [HttpPost]
    public async Task<ActionResult<ContratoDto>> Create(CreateContratoDto dto)
    {
        var contrato = await _contratoService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = contrato.Id }, contrato);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ContratoDto>> Update(int id, UpdateContratoDto dto)
    {
        var contrato = await _contratoService.UpdateAsync(id, dto);
        return Ok(contrato);
    }

    [HttpPut("{id:int}/medicao-config")]
    public async Task<ActionResult<ContratoMedicaoConfigDto>> AtualizarMedicaoConfig(int id, UpdateContratoMedicaoConfigDto dto)
    {
        var config = await _contratoService.AtualizarMedicaoConfigAsync(id, dto);
        return Ok(config);
    }

    [HttpPut("{id:int}/faturamento-config")]
    public async Task<ActionResult<ContratoFaturamentoConfigDto>> AtualizarFaturamentoConfig(int id, UpdateContratoFaturamentoConfigDto dto)
    {
        var config = await _contratoService.AtualizarFaturamentoConfigAsync(id, dto);
        return Ok(config);
    }

    [HttpGet("{id:int}/anexos")]
    public async Task<ActionResult<List<AnexoDto>>> ListarAnexos(int id)
    {
        var anexos = await _contratoService.ListarAnexosAsync(id);
        return Ok(anexos);
    }

    [HttpPost("{id:int}/anexos")]
    public async Task<ActionResult<AnexoDto>> AdicionarAnexo(int id, IFormFile arquivo)
    {
        if (arquivo is null || arquivo.Length == 0)
        {
            return BadRequest(new { message = "Nenhum arquivo enviado." });
        }

        using var stream = new MemoryStream();
        await arquivo.CopyToAsync(stream);

        var anexo = await _contratoService.AdicionarAnexoAsync(id, new AdicionarAnexoDto
        {
            NomeArquivo = arquivo.FileName,
            TipoConteudo = arquivo.ContentType,
            Conteudo = stream.ToArray(),
        });

        return Ok(anexo);
    }

    [HttpGet("{id:int}/anexos/{anexoId:int}")]
    public async Task<IActionResult> BaixarAnexo(int id, int anexoId)
    {
        var arquivo = await _contratoService.ObterAnexoAsync(id, anexoId);
        return File(arquivo.Conteudo, arquivo.TipoConteudo, arquivo.NomeArquivo);
    }

    [HttpDelete("{id:int}/anexos/{anexoId:int}")]
    public async Task<IActionResult> ExcluirAnexo(int id, int anexoId)
    {
        await _contratoService.ExcluirAnexoAsync(id, anexoId);
        return NoContent();
    }

    [HttpGet("{id:int}/aditivos")]
    public async Task<ActionResult<List<AditivoDto>>> ListarAditivos(int id)
    {
        var aditivos = await _contratoService.ListarAditivosAsync(id);
        return Ok(aditivos);
    }

    [HttpPost("{id:int}/aditivos")]
    public async Task<ActionResult<AditivoDto>> CriarAditivo(int id, CreateAditivoDto dto)
    {
        var aditivo = await _contratoService.CriarAditivoAsync(id, dto);
        return Ok(aditivo);
    }

    [HttpPatch("{id:int}/aditivos/{aditivoId:int}/formalizar")]
    public async Task<ActionResult<AditivoDto>> FormalizarAditivo(int id, int aditivoId)
    {
        var aditivo = await _contratoService.FormalizarAditivoAsync(id, aditivoId);
        return Ok(aditivo);
    }

    [HttpGet("{id:int}/medicoes")]
    public async Task<ActionResult<List<MedicaoBmDto>>> ListarMedicoes(int id)
    {
        var medicoes = await _contratoService.ListarMedicoesAsync(id);
        return Ok(medicoes);
    }

    [HttpGet("{id:int}/medicoes/{medicaoId:int}")]
    public async Task<ActionResult<MedicaoBmDto>> ObterMedicao(int id, int medicaoId)
    {
        var medicao = await _contratoService.ObterMedicaoAsync(id, medicaoId);
        return Ok(medicao);
    }

    [HttpPost("{id:int}/medicoes")]
    public async Task<ActionResult<MedicaoBmDto>> CriarMedicao(int id, CreateMedicaoBmDto dto)
    {
        var medicao = await _contratoService.CriarMedicaoBmAsync(id, dto);
        return Ok(medicao);
    }

    [HttpPut("{id:int}/medicoes/{medicaoId:int}")]
    public async Task<ActionResult<MedicaoBmDto>> AtualizarMedicao(int id, int medicaoId, UpdateMedicaoBmDto dto)
    {
        var medicao = await _contratoService.AtualizarMedicaoBmAsync(id, medicaoId, dto);
        return Ok(medicao);
    }

    [HttpPatch("{id:int}/medicoes/{medicaoId:int}/aprovar")]
    public async Task<ActionResult<MedicaoBmDto>> AprovarMedicao(int id, int medicaoId)
    {
        // Qualquer Administrador pode aprovar, mesmo sem Usuario (colaborador) vinculado à conta
        // de acesso — diferente do fluxo de Reembolso, que exige um aprovador de setor específico.
        var medicao = await _contratoService.AprovarMedicaoBmAsync(id, medicaoId, User.ObterUsuarioId());
        return Ok(medicao);
    }

    [HttpPatch("{id:int}/medicoes/{medicaoId:int}/reprovar")]
    public async Task<ActionResult<MedicaoBmDto>> ReprovarMedicao(int id, int medicaoId, ReprovarMedicaoBmDto dto)
    {
        var medicao = await _contratoService.ReprovarMedicaoBmAsync(id, medicaoId, User.ObterUsuarioId(), dto);
        return Ok(medicao);
    }

    [HttpGet("{id:int}/saldo")]
    public async Task<ActionResult<List<ContratoSaldoItemDto>>> ObterSaldo(int id)
    {
        var saldo = await _contratoService.ObterSaldoAsync(id);
        return Ok(saldo);
    }

    [HttpGet("{id:int}/medicoes/{medicaoId:int}/anexos")]
    public async Task<ActionResult<List<AnexoDto>>> ListarAnexosMedicao(int id, int medicaoId)
    {
        var anexos = await _contratoService.ListarAnexosMedicaoAsync(id, medicaoId);
        return Ok(anexos);
    }

    [HttpPost("{id:int}/medicoes/{medicaoId:int}/anexos")]
    public async Task<ActionResult<AnexoDto>> AdicionarAnexoMedicao(int id, int medicaoId, IFormFile arquivo)
    {
        if (arquivo is null || arquivo.Length == 0)
        {
            return BadRequest(new { message = "Nenhum arquivo enviado." });
        }

        using var stream = new MemoryStream();
        await arquivo.CopyToAsync(stream);

        var anexo = await _contratoService.AdicionarAnexoMedicaoAsync(id, medicaoId, new AdicionarAnexoDto
        {
            NomeArquivo = arquivo.FileName,
            TipoConteudo = arquivo.ContentType,
            Conteudo = stream.ToArray(),
        });

        return Ok(anexo);
    }

    [HttpGet("{id:int}/medicoes/{medicaoId:int}/anexos/{anexoId:int}")]
    public async Task<IActionResult> BaixarAnexoMedicao(int id, int medicaoId, int anexoId)
    {
        var arquivo = await _contratoService.ObterAnexoMedicaoAsync(id, medicaoId, anexoId);
        return File(arquivo.Conteudo, arquivo.TipoConteudo, arquivo.NomeArquivo);
    }

    [HttpDelete("{id:int}/medicoes/{medicaoId:int}/anexos/{anexoId:int}")]
    public async Task<IActionResult> ExcluirAnexoMedicao(int id, int medicaoId, int anexoId)
    {
        await _contratoService.ExcluirAnexoMedicaoAsync(id, medicaoId, anexoId);
        return NoContent();
    }
}

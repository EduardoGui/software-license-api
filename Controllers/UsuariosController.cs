using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SoftwareLicense.Api.DTOs;
using SoftwareLicense.Api.Extensions;
using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Controllers;

[ApiController]
[Route("api/usuarios")]
public class UsuariosController : ControllerBase
{
    private readonly IUsuarioService _usuarioService;

    public UsuariosController(IUsuarioService usuarioService)
    {
        _usuarioService = usuarioService;
    }

    // Listagem/detalhe liberados pra Administrativo também - a maioria das telas de DP/Patrimônio
    // precisa selecionar um colaborador (reembolso, alocação, férias etc). Criar/editar/desativar
    // Usuário e seus dependentes continuam só Administrador - gestão de colaborador em si fica
    // fora do escopo do Administrativo.
    [HttpGet]
    [Authorize(Roles = $"{Roles.Administrador},{Roles.Administrativo}")]
    public async Task<ActionResult<List<UsuarioDto>>> GetAll([FromQuery] UsuarioFiltroDto filtro)
    {
        var usuarios = await _usuarioService.GetAllAsync(filtro);
        return Ok(usuarios);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = $"{Roles.Administrador},{Roles.Administrativo},{Roles.Colaborador}")]
    public async Task<ActionResult<UsuarioDto>> GetById(int id)
    {
        if (!User.EhAdminOuAdministrativo() && !User.TemUsuarioId(id))
        {
            return Forbid();
        }

        var usuario = await _usuarioService.GetByIdAsync(id);
        return Ok(usuario);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<UsuarioDto>> Create(CreateUsuarioDto dto)
    {
        var usuario = await _usuarioService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = usuario.Id }, usuario);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<UsuarioDto>> Update(int id, UpdateUsuarioDto dto)
    {
        var usuario = await _usuarioService.UpdateAsync(id, dto);
        return Ok(usuario);
    }

    [HttpPatch("{id:int}/desativar")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<UsuarioDto>> Desativar(int id, DesativarUsuarioDto dto)
    {
        var usuario = await _usuarioService.DesativarAsync(id, dto);
        return Ok(usuario);
    }

    [HttpPatch("{id:int}/perfil")]
    [Authorize(Roles = $"{Roles.Administrador},{Roles.Colaborador}")]
    public async Task<ActionResult<UsuarioDto>> AtualizarPerfil(int id, AtualizarPerfilDto dto)
    {
        if (!User.IsInRole(Roles.Administrador) && !User.TemUsuarioId(id))
        {
            return Forbid();
        }

        var usuario = await _usuarioService.AtualizarPerfilAsync(id, dto);
        return Ok(usuario);
    }

    [HttpPatch("{id:int}/reenviar-convite")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<IActionResult> ReenviarConvite(int id)
    {
        await _usuarioService.ReenviarConviteAsync(id);
        return NoContent();
    }

    [HttpPost("{id:int}/dependentes")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<UsuarioDto>> AdicionarDependente(int id, CreateDependenteDto dto)
    {
        var usuario = await _usuarioService.AdicionarDependenteAsync(id, dto);
        return Ok(usuario);
    }

    [HttpPut("{id:int}/dependentes/{dependenteId:int}")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<UsuarioDto>> AtualizarDependente(int id, int dependenteId, UpdateDependenteDto dto)
    {
        var usuario = await _usuarioService.AtualizarDependenteAsync(id, dependenteId, dto);
        return Ok(usuario);
    }

    [HttpDelete("{id:int}/dependentes/{dependenteId:int}")]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<ActionResult<UsuarioDto>> RemoverDependente(int id, int dependenteId)
    {
        var usuario = await _usuarioService.RemoverDependenteAsync(id, dependenteId);
        return Ok(usuario);
    }

    // Listar/baixar: Administrador, Administrativo ou o próprio colaborador (mobile só visualiza,
    // ver "Meus dados"). Incluir/excluir: Administrador ou Administrativo (Termo de
    // Responsabilidade é assunto de DP) - upload é feito só pela web admin.
    [HttpGet("{id:int}/anexos")]
    [Authorize(Roles = $"{Roles.Administrador},{Roles.Administrativo},{Roles.Colaborador}")]
    public async Task<ActionResult<List<AnexoDto>>> ListarAnexos(int id)
    {
        if (!User.EhAdminOuAdministrativo() && !User.TemUsuarioId(id))
        {
            return Forbid();
        }

        var anexos = await _usuarioService.ListarAnexosAsync(id);
        return Ok(anexos);
    }

    [HttpPost("{id:int}/anexos")]
    [Authorize(Roles = $"{Roles.Administrador},{Roles.Administrativo}")]
    public async Task<ActionResult<AnexoDto>> AdicionarAnexo(int id, IFormFile arquivo)
    {
        if (arquivo is null || arquivo.Length == 0)
        {
            return BadRequest(new { message = "Nenhum arquivo enviado." });
        }

        using var stream = new MemoryStream();
        await arquivo.CopyToAsync(stream);

        var anexo = await _usuarioService.AdicionarAnexoAsync(id, new AdicionarAnexoDto
        {
            NomeArquivo = arquivo.FileName,
            TipoConteudo = arquivo.ContentType,
            Conteudo = stream.ToArray(),
        });

        return Ok(anexo);
    }

    [HttpGet("{id:int}/anexos/{anexoId:int}")]
    [Authorize(Roles = $"{Roles.Administrador},{Roles.Administrativo},{Roles.Colaborador}")]
    public async Task<IActionResult> BaixarAnexo(int id, int anexoId)
    {
        if (!User.EhAdminOuAdministrativo() && !User.TemUsuarioId(id))
        {
            return Forbid();
        }

        var arquivo = await _usuarioService.ObterAnexoAsync(id, anexoId);
        return File(arquivo.Conteudo, arquivo.TipoConteudo, arquivo.NomeArquivo);
    }

    [HttpDelete("{id:int}/anexos/{anexoId:int}")]
    [Authorize(Roles = $"{Roles.Administrador},{Roles.Administrativo}")]
    public async Task<IActionResult> ExcluirAnexo(int id, int anexoId)
    {
        await _usuarioService.ExcluirAnexoAsync(id, anexoId);
        return NoContent();
    }
}

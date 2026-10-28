using SoftwareLicense.Api.Extensions;

namespace SoftwareLicense.Api.Services;

// Escopo de dados do usuário logado. O perfil Administrativo (sem Administrador) só enxerga e mexe no que foi
// criado pela administração (OCs e despesas avulsas com CriadaPelaAdministracao = true, e suas obrigações).
// Sem escopo (null) ou Administrador = sem restrição.
public interface IEscopoAcesso
{
    bool RestritoAAdministracao { get; }
    int? UsuarioId { get; }
}

public class EscopoAcessoHttp : IEscopoAcesso
{
    private readonly IHttpContextAccessor _accessor;

    public EscopoAcessoHttp(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public bool RestritoAAdministracao =>
        _accessor.HttpContext?.User is { Identity.IsAuthenticated: true } user
        && user.IsInRole(Roles.Administrativo)
        && !user.IsInRole(Roles.Administrador);

    public int? UsuarioId => _accessor.HttpContext?.User.ObterUsuarioId();
}

using System.Security.Claims;

namespace SoftwareLicense.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    // Verifica se o usuário autenticado é o colaborador dono do usuarioId informado
    // (claim "usuarioId" só existe em contas de acesso vinculadas a um Usuario/Colaborador).
    public static bool TemUsuarioId(this ClaimsPrincipal principal, int usuarioId)
    {
        var valor = principal.FindFirstValue("usuarioId");
        return int.TryParse(valor, out var id) && id == usuarioId;
    }

    // Retorna o UsuarioId vinculado à conta autenticada, ou null se a conta não tiver um
    // (ex.: conta de Administrador sem colaborador associado).
    public static int? ObterUsuarioId(this ClaimsPrincipal principal)
    {
        var valor = principal.FindFirstValue("usuarioId");
        return int.TryParse(valor, out var id) ? id : null;
    }

    // Administrativo tem o mesmo nível de operação de um Administrador nas telas liberadas pra
    // ele (DP, Patrimônio, Locais/Empresas PJ/Notas Fiscais) - usado nos checks internos desses
    // controllers no lugar de checar só Roles.Administrador.
    public static bool EhAdminOuAdministrativo(this ClaimsPrincipal principal) =>
        principal.IsInRole(Services.Roles.Administrador) || principal.IsInRole(Services.Roles.Administrativo);
}

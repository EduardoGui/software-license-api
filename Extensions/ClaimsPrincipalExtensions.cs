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
}

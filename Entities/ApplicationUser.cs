using Microsoft.AspNetCore.Identity;

namespace SoftwareLicense.Api.Entities;

public class ApplicationUser : IdentityUser
{
    public int? UsuarioId { get; set; }

    public Usuario? Usuario { get; set; }

    // Gravado a cada login bem-sucedido (UTC); null = nunca entrou.
    public DateTime? UltimoAcessoEm { get; set; }
}

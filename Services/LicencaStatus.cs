using SoftwareLicense.Api.Entities;

namespace SoftwareLicense.Api.Services;

public static class LicencaStatus
{
    public const string Ativa = "Ativa";
    public const string Inativa = "Inativa";

    public static string Calcular(Licenca licenca) => licenca.Ativa ? Ativa : Inativa;
}

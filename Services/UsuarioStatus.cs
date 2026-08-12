using SoftwareLicense.Api.Entities;

namespace SoftwareLicense.Api.Services;

public static class UsuarioStatus
{
    public const string Agendado = "Agendado";
    public const string Ativo = "Ativo";
    public const string Inativo = "Inativo";

    public static string Calcular(Usuario usuario, DateOnly hoje)
    {
        if (usuario.DataInicio > hoje)
        {
            return Agendado;
        }

        if (usuario.DataFim is null || usuario.DataFim > hoje)
        {
            return Ativo;
        }

        return Inativo;
    }
}

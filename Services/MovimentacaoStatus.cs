using SoftwareLicense.Api.Entities;

namespace SoftwareLicense.Api.Services;

public static class MovimentacaoStatus
{
    public const string EmUso = "Em uso";
    public const string Encerrado = "Encerrado";

    public static string Calcular(UsuarioLicenca movimentacao) => movimentacao.DataFim is null ? EmUso : Encerrado;
}

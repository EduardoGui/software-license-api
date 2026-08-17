using SoftwareLicense.Api.Entities;

namespace SoftwareLicense.Api.Services;

public static class EquipamentoAlocacaoStatus
{
    public const string EmUso = "Em uso";
    public const string Encerrado = "Encerrado";

    public static string Calcular(EquipamentoAlocacao alocacao) => alocacao.DataFim is null ? EmUso : Encerrado;
}

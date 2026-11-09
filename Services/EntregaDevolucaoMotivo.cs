namespace SoftwareLicense.Api.Services;

public static class EntregaDevolucaoMotivo
{
    public const string Troca = "Troca";
    public const string Devolucao = "Devolucao";
    public const string Defeito = "Defeito";
    public const string Outro = "Outro";
    public static readonly string[] Validos = [Troca, Devolucao, Defeito, Outro];
}

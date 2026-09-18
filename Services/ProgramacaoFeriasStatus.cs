namespace SoftwareLicense.Api.Services;

public static class ProgramacaoFeriasStatus
{
    // Gravados no banco.
    public const string Rascunho = "Rascunho";
    public const string Solicitada = "Solicitada";
    public const string Aprovada = "Aprovada";
    public const string Reprovada = "Reprovada";
    public const string Cancelada = "Cancelada";

    // Nunca gravados - só existem como "status efetivo" calculado na leitura a partir das datas,
    // quando o status gravado é Aprovada (ver ProgramacaoFeriasService.CalcularStatusEfetivo).
    public const string EmGozo = "EmGozo";
    public const string Concluida = "Concluida";
}

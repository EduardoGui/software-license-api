namespace SoftwareLicense.Api.Services;

public static class UsuarioTipo
{
    public const string Pj = "Pj";
    public const string Clt = "Clt";
    public const string Estagio = "Estagio";

    // Tipos de vínculo que hoje têm suporte no módulo de Férias (Estagio fica de fora - regra de
    // acúmulo é outra, 15 dias a cada 6 meses de estágio, não encaixa no motor atual).
    public static readonly string[] ComModuloDeFerias = [Pj, Clt];
}

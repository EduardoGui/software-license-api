using SoftwareLicense.Api.Exceptions;

namespace SoftwareLicense.Api.Services;

public static class AnexoValidator
{
    private static readonly HashSet<string> TiposPermitidos = ["application/pdf", "image/jpeg", "image/png"];
    private const long TamanhoMaximoBytes = 10 * 1024 * 1024;

    public static void Validar(string tipoConteudo, long tamanho)
    {
        if (!TiposPermitidos.Contains(tipoConteudo))
        {
            throw new BusinessRuleException("Tipo de arquivo não permitido. Envie um PDF, JPEG ou PNG.");
        }

        if (tamanho <= 0)
        {
            throw new BusinessRuleException("Arquivo vazio.");
        }

        if (tamanho > TamanhoMaximoBytes)
        {
            throw new BusinessRuleException("Arquivo excede o tamanho máximo permitido (10 MB).");
        }
    }
}

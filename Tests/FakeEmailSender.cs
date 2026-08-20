using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Tests;

public class FakeEmailSender : IEmailSender
{
    public int ChamadasSimples { get; private set; }
    public int ChamadasComAnexo { get; private set; }
    public IReadOnlyList<string>? UltimosDestinatarios { get; private set; }
    public IReadOnlyList<string>? UltimaCopia { get; private set; }
    public IReadOnlyList<EmailAnexo>? UltimosAnexos { get; private set; }

    public Task EnviarAsync(string destinatario, string assunto, string corpoHtml)
    {
        ChamadasSimples++;
        return Task.CompletedTask;
    }

    public Task EnviarAsync(
        IReadOnlyList<string> destinatarios, string assunto, string corpoHtml,
        IReadOnlyList<string>? copia = null, IReadOnlyList<EmailAnexo>? anexos = null)
    {
        ChamadasComAnexo++;
        UltimosDestinatarios = destinatarios;
        UltimaCopia = copia;
        UltimosAnexos = anexos;
        return Task.CompletedTask;
    }
}

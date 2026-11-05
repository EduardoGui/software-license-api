using SoftwareLicense.Api.Services;

namespace SoftwareLicense.Api.Tests;

public class FakeEmailSender : IEmailSender
{
    public int ChamadasSimples { get; private set; }
    public string? UltimoDestinatarioSimples { get; private set; }
    public string? UltimoAssuntoSimples { get; private set; }
    public string? UltimoCorpoSimples { get; private set; }
    public int ChamadasComAnexo { get; private set; }
    public IReadOnlyList<string>? UltimosDestinatarios { get; private set; }
    public IReadOnlyList<string>? UltimaCopia { get; private set; }
    public IReadOnlyList<EmailAnexo>? UltimosAnexos { get; private set; }
    public string? UltimoAssuntoComAnexo { get; private set; }
    public string? UltimoCorpoComAnexo { get; private set; }

    public void Reiniciar()
    {
        ChamadasSimples = 0;
        ChamadasComAnexo = 0;
        UltimoDestinatarioSimples = UltimoAssuntoSimples = UltimoCorpoSimples = null;
        UltimoAssuntoComAnexo = UltimoCorpoComAnexo = null;
        UltimosDestinatarios = UltimaCopia = null;
        UltimosAnexos = null;
    }

    public Task EnviarAsync(string destinatario, string assunto, string corpoHtml)
    {
        ChamadasSimples++;
        UltimoDestinatarioSimples = destinatario;
        UltimoAssuntoSimples = assunto;
        UltimoCorpoSimples = corpoHtml;
        return Task.CompletedTask;
    }

    public Task EnviarAsync(
        IReadOnlyList<string> destinatarios, string assunto, string corpoHtml,
        IReadOnlyList<string>? copia = null, IReadOnlyList<EmailAnexo>? anexos = null)
    {
        ChamadasComAnexo++;
        UltimoAssuntoComAnexo = assunto;
        UltimoCorpoComAnexo = corpoHtml;
        UltimosDestinatarios = destinatarios;
        UltimaCopia = copia;
        UltimosAnexos = anexos;
        return Task.CompletedTask;
    }
}

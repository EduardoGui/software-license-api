namespace SoftwareLicense.Api.Services;

public interface IEmailSender
{
    Task EnviarAsync(string destinatario, string assunto, string corpoHtml);

    Task EnviarAsync(
        IReadOnlyList<string> destinatarios, string assunto, string corpoHtml,
        IReadOnlyList<string>? copia = null, IReadOnlyList<EmailAnexo>? anexos = null);
}

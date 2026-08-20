namespace SoftwareLicense.Api.Services;

// Implementação provisória: registra o e-mail no log em vez de enviar de verdade.
// Será substituída pelo envio real via SMTP (Brevo) na etapa 13.3 do Plano 13.
public class LogEmailSender : IEmailSender
{
    private readonly ILogger<LogEmailSender> _logger;

    public LogEmailSender(ILogger<LogEmailSender> logger)
    {
        _logger = logger;
    }

    public Task EnviarAsync(string destinatario, string assunto, string corpoHtml)
    {
        _logger.LogInformation(
            "[E-mail simulado — SMTP ainda não configurado] Para: {Destinatario} | Assunto: {Assunto}\n{Corpo}",
            destinatario, assunto, corpoHtml);
        return Task.CompletedTask;
    }

    public Task EnviarAsync(
        IReadOnlyList<string> destinatarios, string assunto, string corpoHtml,
        IReadOnlyList<string>? copia = null, IReadOnlyList<EmailAnexo>? anexos = null)
    {
        _logger.LogInformation(
            "[E-mail simulado — SMTP ainda não configurado] Para: {Destinatarios} | Cc: {Copia} | Assunto: {Assunto} | Anexos: {QuantidadeAnexos}\n{Corpo}",
            string.Join(", ", destinatarios), copia is null ? "-" : string.Join(", ", copia), assunto, anexos?.Count ?? 0, corpoHtml);
        return Task.CompletedTask;
    }
}

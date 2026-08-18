using System.Net;
using System.Net.Mail;

namespace SoftwareLicense.Api.Services;

// System.Net.Mail.SmtpClient é nativo do .NET (sem dependência nova) e suficiente
// para o envio pontual de e-mails transacionais deste projeto (convite de senha).
public class SmtpEmailSender : IEmailSender
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _usuario;
    private readonly string _senha;
    private readonly string _remetenteEmail;
    private readonly string _remetenteNome;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _host = configuration["Smtp:Host"]
            ?? throw new InvalidOperationException("Configuração 'Smtp:Host' não encontrada.");
        _port = int.TryParse(configuration["Smtp:Port"], out var porta) ? porta : 587;
        _usuario = configuration["Smtp:Usuario"]
            ?? throw new InvalidOperationException("Configuração 'Smtp:Usuario' não encontrada.");
        _senha = configuration["Smtp:Senha"]
            ?? throw new InvalidOperationException("Configuração 'Smtp:Senha' não encontrada.");
        _remetenteEmail = configuration["Smtp:RemetenteEmail"] ?? _usuario;
        _remetenteNome = configuration["Smtp:RemetenteNome"] ?? "Adm Hope";
        _logger = logger;
    }

    public async Task EnviarAsync(string destinatario, string assunto, string corpoHtml)
    {
        using var client = new SmtpClient(_host, _port)
        {
            Credentials = new NetworkCredential(_usuario, _senha),
            EnableSsl = true,
        };

        using var mensagem = new MailMessage
        {
            From = new MailAddress(_remetenteEmail, _remetenteNome),
            Subject = assunto,
            Body = corpoHtml,
            IsBodyHtml = true,
        };
        mensagem.To.Add(destinatario);

        await client.SendMailAsync(mensagem);

        _logger.LogInformation("E-mail enviado para {Destinatario}", destinatario);
    }
}

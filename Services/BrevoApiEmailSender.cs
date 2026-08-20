using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SoftwareLicense.Api.Services;

// Envia e-mails via API HTTP da Brevo (não SMTP). Plataformas de hospedagem como o Render
// bloqueiam conexões SMTP diretas por padrão (medida antispam), mas permitem chamadas HTTPS
// normais - a API funciona igual em qualquer ambiente, local ou em nuvem.
public class BrevoApiEmailSender : IEmailSender
{
    private static readonly JsonSerializerOptions JsonOpcoes = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly string _remetenteEmail;
    private readonly string _remetenteNome;
    private readonly ILogger<BrevoApiEmailSender> _logger;

    public BrevoApiEmailSender(HttpClient http, IConfiguration configuration, ILogger<BrevoApiEmailSender> logger)
    {
        var apiKey = configuration["Brevo:ApiKey"]
            ?? throw new InvalidOperationException("Configuração 'Brevo:ApiKey' não encontrada.");
        _remetenteEmail = configuration["Brevo:RemetenteEmail"]
            ?? throw new InvalidOperationException("Configuração 'Brevo:RemetenteEmail' não encontrada.");
        _remetenteNome = configuration["Brevo:RemetenteNome"] ?? "Adm Hope";
        _logger = logger;

        _http = http;
        _http.BaseAddress = new Uri("https://api.brevo.com/v3/");
        _http.DefaultRequestHeaders.Add("api-key", apiKey);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public Task EnviarAsync(string destinatario, string assunto, string corpoHtml) =>
        EnviarAsync([destinatario], assunto, corpoHtml);

    public async Task EnviarAsync(
        IReadOnlyList<string> destinatarios, string assunto, string corpoHtml,
        IReadOnlyList<string>? copia = null, IReadOnlyList<EmailAnexo>? anexos = null)
    {
        var payload = new BrevoEmailRequest
        {
            Sender = new BrevoContato(_remetenteNome, _remetenteEmail),
            To = destinatarios.Select(d => new BrevoContato(null, d)).ToList(),
            Cc = copia is { Count: > 0 } ? copia.Select(c => new BrevoContato(null, c)).ToList() : null,
            Subject = assunto,
            HtmlContent = corpoHtml,
            Attachment = anexos is { Count: > 0 }
                ? anexos.Select(a => new BrevoAnexo(Convert.ToBase64String(a.Conteudo), a.NomeArquivo)).ToList()
                : null,
        };

        var resposta = await _http.PostAsJsonAsync("smtp/email", payload, JsonOpcoes);
        if (!resposta.IsSuccessStatusCode)
        {
            var corpo = await resposta.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Falha ao enviar e-mail via API da Brevo ({(int)resposta.StatusCode}): {corpo}");
        }

        _logger.LogInformation(
            "E-mail enviado via API Brevo para {QuantidadeDestinatarios} destinatário(s), {QuantidadeCopia} em cópia, {QuantidadeAnexos} anexo(s)",
            destinatarios.Count, copia?.Count ?? 0, anexos?.Count ?? 0);
    }

    private record BrevoEmailRequest
    {
        [JsonPropertyName("sender")] public required BrevoContato Sender { get; init; }
        [JsonPropertyName("to")] public required List<BrevoContato> To { get; init; }
        [JsonPropertyName("cc")] public List<BrevoContato>? Cc { get; init; }
        [JsonPropertyName("subject")] public required string Subject { get; init; }
        [JsonPropertyName("htmlContent")] public required string HtmlContent { get; init; }
        [JsonPropertyName("attachment")] public List<BrevoAnexo>? Attachment { get; init; }
    }

    private record BrevoContato(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("email")] string Email);

    private record BrevoAnexo(
        [property: JsonPropertyName("content")] string Content,
        [property: JsonPropertyName("name")] string Name);
}

namespace SoftwareLicense.Api.Services;

// O servidor roda em UTC; saudação ("bom dia") e "hoje" do pedido de pagamento seguem o horário de Brasília.
public static class HorarioBrasilia
{
    private static readonly TimeZoneInfo Fuso = ObterFuso();

    public static DateTime Agora(TimeProvider timeProvider) =>
        TimeZoneInfo.ConvertTimeFromUtc(timeProvider.GetUtcNow().UtcDateTime, Fuso);

    public static DateOnly Hoje(TimeProvider timeProvider) => DateOnly.FromDateTime(Agora(timeProvider));

    public static string Saudacao(DateTime horaLocal) => horaLocal.Hour switch
    {
        < 12 => "bom dia",
        < 18 => "boa tarde",
        _ => "boa noite",
    };

    private static TimeZoneInfo ObterFuso()
    {
        foreach (var id in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        // Sem tzdata no servidor: Brasília é UTC-3 fixo (sem horário de verão desde 2019).
        return TimeZoneInfo.CreateCustomTimeZone("Brasilia", TimeSpan.FromHours(-3), "Brasilia", "Brasilia");
    }
}

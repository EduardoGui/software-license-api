namespace SoftwareLicense.Api.Services;

// Regras de calendário para o pedido de pagamento: dias úteis (sem fim de semana e sem feriados cadastrados)
// e a "terça ou quinta" de pagamento.
public static class DiasUteis
{
    public static bool EhDiaUtil(DateOnly data, ISet<DateOnly> feriados) =>
        data.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !feriados.Contains(data);

    // Dias úteis a partir do dia seguinte a "inicio" até "fim" (inclusive). Zero se fim <= inicio.
    public static int Contar(DateOnly inicio, DateOnly fim, ISet<DateOnly> feriados)
    {
        var total = 0;
        for (var dia = inicio.AddDays(1); dia <= fim; dia = dia.AddDays(1))
        {
            if (EhDiaUtil(dia, feriados))
            {
                total++;
            }
        }

        return total;
    }

    // A terça ou quinta mais próxima ESTRITAMENTE anterior ao vencimento; se for feriado, recua para a anterior.
    public static DateOnly TercaOuQuintaAnterior(DateOnly vencimento, ISet<DateOnly> feriados)
    {
        var dia = vencimento.AddDays(-1);
        while (!(dia.DayOfWeek is DayOfWeek.Tuesday or DayOfWeek.Thursday) || feriados.Contains(dia))
        {
            dia = dia.AddDays(-1);
        }

        return dia;
    }
}

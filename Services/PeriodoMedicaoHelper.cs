namespace SoftwareLicense.Api.Services;

// Cálculo do período de medição corrente de um contrato, a partir só do "dia final do período"
// configurado (ex.: período de 16 a 15 do mês seguinte -> DiaFimPeriodo = 15). Compartilhado entre
// DashboardService (alertas) e TarefaOcorrenciaService (lembrete real de "iniciar medição").
public static class PeriodoMedicaoHelper
{
    public static DateOnly FimDoPeriodoCorrente(DateOnly hoje, int diaFimPeriodo)
    {
        var fim = ClampAoMes(hoje.Year, hoje.Month, diaFimPeriodo);
        if (hoje > fim)
        {
            var proximoMes = hoje.AddMonths(1);
            fim = ClampAoMes(proximoMes.Year, proximoMes.Month, diaFimPeriodo);
        }

        return fim;
    }

    // Fim do período imediatamente anterior ao corrente (um mês antes, no mesmo "dia fim").
    public static DateOnly FimDoPeriodoAnterior(DateOnly fimDoPeriodoCorrente, int diaFimPeriodo)
    {
        var mesAnterior = fimDoPeriodoCorrente.AddMonths(-1);
        return ClampAoMes(mesAnterior.Year, mesAnterior.Month, diaFimPeriodo);
    }

    public static DateOnly ClampAoMes(int ano, int mes, int dia) =>
        new(ano, mes, Math.Min(dia, DateTime.DaysInMonth(ano, mes)));
}

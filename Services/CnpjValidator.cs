using System.Text.RegularExpressions;

namespace SoftwareLicense.Api.Services;

public static partial class CnpjValidator
{
    public static string SomenteDigitos(string cnpj) => NaoDigito().Replace(cnpj, "");

    public static bool EhValido(string cnpj)
    {
        var digitos = SomenteDigitos(cnpj);

        if (digitos.Length != 14 || digitos.Distinct().Count() == 1)
        {
            return false;
        }

        var numeros = digitos.Select(c => c - '0').ToArray();

        var dv1 = CalcularDigitoVerificador(numeros[..12], [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        if (dv1 != numeros[12])
        {
            return false;
        }

        var dv2 = CalcularDigitoVerificador(numeros[..13], [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        return dv2 == numeros[13];
    }

    private static int CalcularDigitoVerificador(int[] numeros, int[] pesos)
    {
        var soma = numeros.Zip(pesos, (n, p) => n * p).Sum();
        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex NaoDigito();
}

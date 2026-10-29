namespace SoftwareLicense.Api.Services;

public static class CpfValidator
{
    public static bool EhValido(string cpf)
    {
        var digitos = CnpjValidator.SomenteDigitos(cpf);

        if (digitos.Length != 11 || digitos.Distinct().Count() == 1)
        {
            return false;
        }

        var numeros = digitos.Select(c => c - '0').ToArray();

        var dv1 = CalcularDigitoVerificador(numeros[..9], 10);
        if (dv1 != numeros[9])
        {
            return false;
        }

        var dv2 = CalcularDigitoVerificador(numeros[..10], 11);
        return dv2 == numeros[10];
    }

    private static int CalcularDigitoVerificador(int[] numeros, int pesoInicial)
    {
        var soma = numeros.Select((n, i) => n * (pesoInicial - i)).Sum();
        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}

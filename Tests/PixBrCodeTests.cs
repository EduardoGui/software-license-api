using SoftwareLicense.Api.Services;
using Xunit;

namespace SoftwareLicense.Api.Tests;

public class PixBrCodeTests
{
    [Fact]
    public void CalcularCrc16_DeveBaterComOValorDeVerificacaoPadraoDoCrc16CcittFalse()
    {
        // "123456789" é a string de verificação canônica da família de algoritmos CRC —
        // o valor esperado pro CRC-16/CCITT-FALSE (poly 0x1021, init 0xFFFF) é 0x29B1.
        var crc = PixBrCode.CalcularCrc16("123456789");

        Assert.Equal(0x29B1, crc);
    }

    [Fact]
    public void GerarPayload_DeveMontarEstruturaEmvComChaveNormalizadaEValorFormatado()
    {
        var payload = PixBrCode.GerarPayload(
            chave: "63.523.589/0001-22",
            nomeRecebedor: "SPE Hope S.A.",
            cidade: "Belo Horizonte",
            valor: 1234.5m,
            txId: "NOTADEB0007");

        Assert.StartsWith("000201", payload);
        Assert.Contains("0014br.gov.bcb.pix", payload); // GUI do arranjo Pix (id 00 dentro do campo 26), tamanho 14
        Assert.Contains("011463523589000122", payload); // chave (id 01), só com dígitos — sem pontuação do CNPJ
        Assert.Contains("5407" + "1234.50", payload); // valor com 2 casas ("1234.50" tem 7 caracteres), campo id 54 tamanho 07
        Assert.Contains("5802BR", payload);
        Assert.Contains("BELO HORIZONTE", payload);
        Assert.EndsWith(PixBrCode.CalcularCrc16(payload[..^4]).ToString("X4"), payload);
    }

    [Fact]
    public void GerarPayload_DeveNormalizarNomeELimitarTamanho()
    {
        var payload = PixBrCode.GerarPayload(
            chave: "12345678000199",
            nomeRecebedor: "Razão Social Com Acentuação Muito Longa Demais",
            cidade: "São Paulo",
            valor: null,
            txId: "abc-123");

        // Acento removido, maiúsculo, e o nome (id 59) truncado em no máximo 25 caracteres.
        Assert.Contains("6009SAO PAULO", payload); // cidade (id 60), sem acento
        Assert.Contains("5925RAZAO SOCIAL COM ACENTUAC", payload); // nome (id 59) truncado em 25 caracteres
        Assert.DoesNotContain("Ã", payload);
    }
}

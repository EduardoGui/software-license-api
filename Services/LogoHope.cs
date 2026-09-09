using System.Reflection;
using PdfSharp.Drawing;

namespace SoftwareLicense.Api.Services;

// Mesma logo oficial usada no topo do menu do frontend (logo-hope.png), embutida aqui como
// recurso do assembly para poder ser desenhada nos PDFs gerados pelo backend (mesmo padrão
// de PdfFontResolver.cs para as fontes).
public static class LogoHope
{
    private static readonly Assembly Assembly = typeof(LogoHope).Assembly;
    private static byte[]? _bytes;

    public static XImage Obter()
    {
        _bytes ??= CarregarBytes();
        return XImage.FromStream(new MemoryStream(_bytes));
    }

    private static byte[] CarregarBytes()
    {
        using var stream = Assembly.GetManifestResourceStream("Assets.logo-hope.png")
            ?? throw new InvalidOperationException("Logo embutida 'Assets.logo-hope.png' não encontrada.");
        using var memoria = new MemoryStream();
        stream.CopyTo(memoria);
        return memoria.ToArray();
    }
}

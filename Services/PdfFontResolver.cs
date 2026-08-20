using System.Reflection;
using PdfSharp.Fonts;

namespace SoftwareLicense.Api.Services;

// PdfSharp 6+ é multiplataforma e não lê fontes do sistema automaticamente - precisa de um
// IFontResolver explícito. Usa DejaVu Sans embutida no projeto (Fonts/, licença Bitstream Vera,
// gratuita para uso/redistribuição) em vez de ler do sistema operacional - funciona igual em
// Windows (dev) e Linux (produção), sem depender de fontes instaladas na máquina.
public class PdfFontResolver : IFontResolver
{
    private static readonly Assembly Assembly = typeof(PdfFontResolver).Assembly;

    public byte[] GetFont(string faceName)
    {
        var recurso = faceName switch
        {
            "DejaVuSans#Bold" => "Fonts.DejaVuSans-Bold.ttf",
            "DejaVuSans#Italic" => "Fonts.DejaVuSans-Italic.ttf",
            "DejaVuSans#BoldItalic" => "Fonts.DejaVuSans-BoldItalic.ttf",
            _ => "Fonts.DejaVuSans.ttf",
        };

        using var stream = Assembly.GetManifestResourceStream(recurso)
            ?? throw new InvalidOperationException($"Fonte embutida '{recurso}' não encontrada.");
        using var memoria = new MemoryStream();
        stream.CopyTo(memoria);
        return memoria.ToArray();
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var faceName = (isBold, isItalic) switch
        {
            (true, true) => "DejaVuSans#BoldItalic",
            (true, false) => "DejaVuSans#Bold",
            (false, true) => "DejaVuSans#Italic",
            _ => "DejaVuSans#Regular",
        };

        return new FontResolverInfo(faceName);
    }
}

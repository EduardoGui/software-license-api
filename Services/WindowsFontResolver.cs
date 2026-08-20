using PdfSharp.Fonts;

namespace SoftwareLicense.Api.Services;

// PdfSharp 6+ é multiplataforma e não lê fontes do sistema automaticamente - precisa de um
// IFontResolver explícito. Lê direto da pasta de fontes do Windows (ambiente atual do projeto,
// só local por enquanto - ver pendência de publicação em nuvem na memória do projeto). Se um dia
// isso rodar em Linux, precisa trocar para um arquivo de fonte embutido no projeto.
public class WindowsFontResolver : IFontResolver
{
    private const string PastaFontes = @"C:\Windows\Fonts";

    public byte[] GetFont(string faceName) => File.ReadAllBytes(Path.Combine(PastaFontes, faceName switch
    {
        "Arial#Bold" => "arialbd.ttf",
        "Arial#Italic" => "ariali.ttf",
        "Arial#BoldItalic" => "arialbi.ttf",
        _ => "arial.ttf",
    }));

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var faceName = (isBold, isItalic) switch
        {
            (true, true) => "Arial#BoldItalic",
            (true, false) => "Arial#Bold",
            (false, true) => "Arial#Italic",
            _ => "Arial#Regular",
        };

        return new FontResolverInfo(faceName);
    }
}

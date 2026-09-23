namespace FEntwumS.SVNRExtension.Tools;

public static class AccessAssetsUtil
{
    private const string TargetDescriptionFile = "target.xml";
    private const string ElfTemplateFile = "template.o";
    private const string ProjectTemplateFolder = "SVNR";

    /// <summary>
    /// Pfad der Zielbeschreibung neben der Erweiterung.
    /// </summary>
    /// <remarks>
    /// Der Stub liefert ihren Inhalt auf Anfrage ueber <c>qXfer:features:read</c> aus. GDB
    /// braucht die Registeraufteilung aber schon, bevor es das erste <c>g</c>-Paket liest,
    /// deshalb verweist die Kommandodatei des Programms zusaetzlich auf diese Datei.
    /// </remarks>
    public static string TargetDescriptionPath()
    {
        return AssetPath(TargetDescriptionFile);
    }

    public static string ElfTemplatePath()
    {
        return AssetPath(ElfTemplateFile);
    }

    public static string ProjectTemplateDirectory()
    {
        return AssetPath(Path.Combine("Templates", ProjectTemplateFolder));
    }

    private static string AssetPath(string relativePath)
    {
        // Assets liegen neben der DLL -> im Dev-Lauf im Session-Ordner, installiert unter Packages/Plugins
        var directory = Path.GetDirectoryName(typeof(AccessAssetsUtil).Assembly.Location)!;

        return Path.Combine(directory, "Assets", relativePath);
    }
}
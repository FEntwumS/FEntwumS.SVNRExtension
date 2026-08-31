using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace FEntwumS.SVNRExtension.Tools;

public class SvnrSettingsHelper
{
    
    public const string DebugKitProperty = "DebugKit";
    public const string DebugKitValue = "SVNR";
    
    public static bool IsSvnrKit(UniversalFpgaProjectRoot project)
    {
        return string.Equals(project.Properties.GetString(DebugKitProperty), DebugKitValue,
            StringComparison.OrdinalIgnoreCase);
    }
    
    public static Task UpdateProjectAsmFile(IProjectFile file)
    // Hier soll der Grüne Harken in der UI gesetzt werden + die Datei als Quelle für Asssemblierung und Debugging genommen werden
    {
        if (file.Root is not UniversalFpgaProjectRoot universalFpgaProjectRoot)
            return Task.CompletedTask;

        var path = GetAsmFile(universalFpgaProjectRoot);

        if (file.RelativePath == path)
            return Task.CompletedTask;

        UpdateProjectProperties(universalFpgaProjectRoot, file.RelativePath);
        return ContainerLocator.Container.Resolve<UniversalFpgaProjectManager>()
            .SaveProjectAsync(universalFpgaProjectRoot);
    }
    
    public static string GetAsmFile(UniversalFpgaProjectRoot project)
    {
        var stored = project.Properties.GetString("SVNR/AsmFile");
        if (string.IsNullOrEmpty(stored)) return "none";

        // Der Wert kann auf einem anderen System in die Projektdatei geschrieben worden sein
        // -> auf das Trennzeichen dieses Systems bringen. Sonst ist "asm\wave.asm" unter macOS
        // ein Dateiname statt eines Pfades, und Path.Combine sucht eine Datei, die es nicht gibt.
        return stored.Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
    }

    private static void UpdateProjectProperties(UniversalFpgaProjectRoot project, string? asmFile)
    {
        var include = project.Properties.GetStringArray("include");
        var hasAsmInclude = false;
        if (include != null)
        {
            foreach (var entry in include)
            {
                if (entry == "*.asm")
                {
                    hasAsmInclude = true;
                    break;
                }
            }
        }

        if (!hasAsmInclude)
            project.Properties.AddToStringArray("include", "*.asm");

        // In der Projektdatei steht immer der Schraegstrich -> sie wandert zwischen Systemen,
        // und file.RelativePath bringt das Trennzeichen der gerade laufenden Plattform mit.
        project.Properties.SetString("SVNR/AsmFile", asmFile?.Replace('\\', '/'));
    }
}
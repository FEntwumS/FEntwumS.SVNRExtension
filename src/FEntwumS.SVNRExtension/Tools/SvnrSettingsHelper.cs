using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace FEntwumS.SVNRExtension.Tools;

public class SvnrSettingsHelper
//TODO auf fehlende Toolchain anpassen
{
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
        return project.Properties.GetString("SVNR/AsmFile") ?? "none";
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

        project.Properties.SetString("SVNR/AsmFile", asmFile);
    }
}
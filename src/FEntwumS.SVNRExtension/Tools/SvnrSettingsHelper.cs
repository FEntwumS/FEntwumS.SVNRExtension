using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace FEntwumS.SVNRExtension.Tools;

public class SvnrSettingsHelper
{
    public static Task UpdateProjectAsmFile(IProjectFile file)
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

    public static bool IsSvnrToolchainActive(UniversalFpgaProjectRoot project)
    {
        return project.Properties.GetString("Toolchain") == "svnr";
    }
    
    public static void UpdateProjectProperties(UniversalFpgaProjectRoot project, string? asmFile)
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
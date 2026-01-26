using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using OneWare.Essentials.Models;
using OneWare.UniversalFpgaProjectSystem;
using OneWare.UniversalFpgaProjectSystem.Models;
using Prism.Ioc;

namespace FEntwumS.SVNRExtension.Tools;

public class SvnrSettingsHelper
{
    private static readonly IImage? _icon = Application.Current!.FindResource(ThemeVariant.Dark, "Svnr.Check") as IImage;
    
    public static Task UpdateProjectAsmFile(IProjectFile file)
    {
        if (file.Root is not UniversalFpgaProjectRoot universalFpgaProjectRoot)
            return Task.CompletedTask;

        var path = GetAsmFile(universalFpgaProjectRoot);
        foreach (var projectFile in file.Root.Files)
        {
            if (_icon != null) projectFile.IconOverlays.Remove(_icon);
        }

        if (_icon != null && !file.IconOverlays.Contains(_icon))
            file.IconOverlays.Add(_icon);

        if (file.RelativePath == path)
            return Task.CompletedTask;

        UpdateProjectProperties(universalFpgaProjectRoot, file.RelativePath);
        return ContainerLocator.Container.Resolve<UniversalFpgaProjectManager>()
            .SaveProjectAsync(universalFpgaProjectRoot);
    }

    public static void SetAsmOverlay(IProjectRoot project)
    {
        if (project is not UniversalFpgaProjectRoot  universalFpgaProjectRoot) 
            return;
        
        var path = GetAsmFile(universalFpgaProjectRoot);
        foreach (var projectFile in universalFpgaProjectRoot.Files)
        {
            if (_icon != null) projectFile.IconOverlays.Remove(_icon);
        }
        
        foreach (var projectFile in universalFpgaProjectRoot.Files)
        {
            if (projectFile.RelativePath.Equals(path))
            {
                projectFile.IconOverlays.Add(_icon!);
                return;
            }
        }
    }

    public static string GetAsmFile(UniversalFpgaProjectRoot project)
    {
        if (!HasProjectProperties(project)) return "none";

        var path = project.Properties["SVNR"]?.AsObject()?["AsmFile"]?.ToString();
        return path ?? "none";
    }
    
    public static void UpdateProjectProperties(UniversalFpgaProjectRoot project, string? constraintFile)
    {
        bool ccfInclude = true;
        var test = project.Properties["Include"]?.AsArray()!;
        foreach (var t in test)
        {
            if (t.ToString() == "*.asm")
                ccfInclude = false;
        }

        if (ccfInclude)
        {
            project.Properties["Include"]?.AsArray().Add("*.asm");
        }

        JsonNode js = new JsonObject();
        if (constraintFile != null)
        {
            js["AsmFile"] = constraintFile;
        }
        else
            js["AsmFile"] = "none";

        project.Properties["SVNR"] = js;
    }
    
    public static bool HasProjectProperties(UniversalFpgaProjectRoot project)
    {
        if (project.Properties.ContainsKey("SVNR"))
        {
            if (project.Properties["SVNR"]?.AsObject().ContainsKey("AsmFile") ?? false)
            {
                return true;
            }
        }

        return false;
    }
    
}
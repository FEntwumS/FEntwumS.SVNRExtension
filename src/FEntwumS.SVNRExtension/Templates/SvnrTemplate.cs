using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Helpers;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace FEntwumS.SVNRExtension.Templates;

public class SvnrTemplate(ILogger logger, IMainDockService mainDockService, IPaths paths) : IFpgaProjectTemplate
{
public string Name => "Base SVNR for .asm programming";

public void FillTemplate(UniversalFpgaProjectRoot root)
{
    var path = Path.Combine(paths.PluginsDirectory, "FEntwumS.SVNRExtension", "Assets", "Templates", "SVNR");

    try
    {
        var name = root.Header.Replace(" ", "");
        TemplateHelper.CopyDirectoryAndReplaceString(path, root.FullPath, ("%PROJECTNAME%", name));
        var topEntity = root.AddFile("top" + ".vhd");
        var asmFile = root.AddFile(Path.Combine("asm", "LED_Example.asm"));

        root.TopEntity = "top.vhd";
        root.Properties.SetString("vhdlStandard", "93c");
        root.Properties.AddToStringArray("Include","*.asm");
        root.Properties.SetString("DebugKit", "SVNR");
        
        _ = mainDockService.OpenFileAsync(topEntity.FullPath);
        _ = mainDockService.OpenFileAsync(asmFile.FullPath);
    }
    catch (Exception e)
    {
        logger.Error(e.Message, e);
    }
}
}

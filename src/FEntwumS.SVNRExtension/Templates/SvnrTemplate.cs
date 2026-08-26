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
    var path = Path.Combine(paths.PluginsDirectory, "FEntwumS.SVNRExtention", "Assets", "SVNR");

    try
    {
        var name = root.Header.Replace(" ", "");
        TemplateHelper.CopyDirectoryAndReplaceString(path, root.FullPath, ("%PROJECTNAME%", name));
        //var file = root.AddFile(name + ".vhd");

        //root.TopEntity = name;
        root.Properties.SetString("vhdlStandard", "93c");

        //var file2 = root.AddFile(name + "_tb.vhd");

        //root.AddTestBench(file2.RelativePath);

        //_ = mainDockService.OpenFileAsync(file.FullPath);
        //_ = mainDockService.OpenFileAsync(file2.FullPath);
    }
    catch (Exception e)
    {
        logger.Error(e.Message, e);
    }
}
}

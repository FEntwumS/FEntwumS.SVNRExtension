using FEntwumS.SVNRExtension.Services;
using OneWare.Essentials.Services;
using OneWare.OssCadSuiteIntegration.Yosys;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace FEntwumS.SVNRExtension;

public class SvnrToolchain(YosysService yosysService) : YosysToolchain(yosysService)
{
    public const string ToolchainId = "svnr";
    public override string Id => ToolchainId;
    
    public override string Name => "SVNR";
    
    public override async Task<bool> CompileAsync(UniversalFpgaProjectRoot project, FpgaModel fpga)
    {
        var svnrToolchainService = ContainerLocator.Container.Resolve<SvnrToolchainService>();
        
        return await svnrToolchainService.CompileAsync(project, fpga);
    }
}
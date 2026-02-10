using FEntwumS.SVNRExtension.Services;
using OneWare.Essentials.Services;
using OneWare.OssCadSuiteIntegration.Yosys;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace FEntwumS.SVNRExtension;

public class SvnrToolchain(YosysToolchain yosysToolchain) : IFpgaToolchain
{
    public const string ToolchainId = "svnr";
    public string Id => ToolchainId;

    public void OnProjectCreated(UniversalFpgaProjectRoot project)
    {
        yosysToolchain.OnProjectCreated(project);
    }

    public void LoadConnections(UniversalFpgaProjectRoot project, FpgaModel fpga)
    {
        yosysToolchain.LoadConnections(project, fpga);
    }

    public void SaveConnections(UniversalFpgaProjectRoot project, FpgaModel fpga)
    {
        yosysToolchain.SaveConnections(project, fpga);
    }

    public async Task<bool> CompileAsync(UniversalFpgaProjectRoot project, FpgaModel fpga)
    {
        var svnrToolchainService = ContainerLocator.Container.Resolve<SvnrToolchainService>();
        
        return await svnrToolchainService.CompileAsync(project, fpga);
    }
    
    public string Name => "SVNR";
}
using FentwumS.SVNRExtension.Services;
using OneWare.OssCadSuiteIntegration.Yosys;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace FentwumS.SVNRExtension;

public class SvnrToolchain(YosysToolchain yosysToolchain, SvnrToolchainService svnrToolchainService) : IFpgaToolchain
{
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
        return await svnrToolchainService.CompileAsync(project, fpga);
    }

    public string Name => "SVNR";
}
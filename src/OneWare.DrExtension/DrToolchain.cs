using OneWare.DrExtension.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.DrExtension;

public class DrToolchain(GhdlToolchainService ghdlToolchain, DRToolchainService drToolchainService) : IFpgaToolchain
{
    public void OnProjectCreated(UniversalFpgaProjectRoot project)
    {
        ghdlToolchain.OnProjectCreated(project);
    }

    public void LoadConnections(UniversalFpgaProjectRoot project, FpgaModel fpga)
    {
        ghdlToolchain.LoadConnections(project, fpga);
    }

    public void SaveConnections(UniversalFpgaProjectRoot project, FpgaModel fpga)
    {
        ghdlToolchain.SaveConnections(project, fpga);
    }

    public async Task<bool> CompileAsync(UniversalFpgaProjectRoot project, FpgaModel fpga)
    {
        return await drToolchainService.CompileAsync(project, fpga);
    }

    public string Name => "DR";
}
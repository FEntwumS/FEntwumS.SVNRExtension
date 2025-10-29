using OneWare.Essentials.Services;
using OneWare.GhdlExtension;
using OneWare.GhdlExtension.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;
using Prism.Ioc;

namespace OneWare.Praktikum4Extension;

public class Praktikum4Toolchain(AsmToVhdlPreCompileStep asmPreCompiler, GhdlYosysToolchain ghdlToolchain, GhdlService ghdlService) : IFpgaToolchain
{
    public void OnProjectCreated(UniversalFpgaProjectRoot project)
    {
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
        bool success = await asmPreCompiler.PerformPreCompileStepAsync(project, fpga);
        if (!success) return false;

        try
        {
            success = await ghdlToolchain.CompileAsync(project, fpga);
            return success;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            return false;
        }
    }

    public string Name => "Praktikum_4";
}
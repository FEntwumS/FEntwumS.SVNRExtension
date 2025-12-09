using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using Prism.Ioc;

namespace OneWare.DrExtension.Services;

public class DRToolchainService(GhdlToolchainService ghdlToolchain, AsmToVhdlPreCompileStep asmPreCompiler)
{
    
    public async Task<bool> CompileAsync(UniversalFpgaProjectRoot project, FpgaModel fpga)
    {
        var success = await SynthAsync(project, fpga);
        success &= await FitAsync(project, fpga);
        success &= await AssembleAsync(project, fpga);
        return success;
    }

    public async Task<bool> SynthAsync(UniversalFpgaProjectRoot project, FpgaModel fpga)
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

    public async Task<bool> FitAsync(UniversalFpgaProjectRoot project, FpgaModel fpga)
    {
        return await ghdlToolchain.FitAsync(project, fpga);
    }

    public async Task<bool> AssembleAsync(UniversalFpgaProjectRoot project, FpgaModel fpga)
    {
        return await ghdlToolchain.AssembleAsync(project, fpga);
    }


}
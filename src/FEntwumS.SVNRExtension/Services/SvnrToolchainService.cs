using FEntwumS.SVNRExtension.Tools;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;
using OneWare.OssCadSuiteIntegration.Yosys;
using OneWare.ProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace FEntwumS.SVNRExtension.Services;

public class SvnrToolchainService(YosysService yosysService, AsmConverterService converterService, ILogger logger)
{
    
    public async Task<bool> CompileAsync(UniversalFpgaProjectRoot project, FpgaModel fpga)
    {
        var success = await SynthAsync(project, fpga);
        success = success && await FitAsync(project, fpga);
        success = success && await AssembleAsync(project, fpga);
        return success;
    }

    public async Task<bool> SynthAsync(UniversalFpgaProjectRoot project, FpgaModel fpga)
    {
        try
        {

            if (!SvnrSettingsHelper.IsSvnrToolchainActive(project))
            {
                return true;
            }
            var asmPath = SvnrSettingsHelper.GetAsmFile(project);
            if (asmPath.Equals("none"))
            {
                throw new Exception("No .asm file found");
            }

            var asmFile = new ProjectFile(asmPath, project.TopFolder!);
            logger.Log(LogLevel.Debug, "Converting .asm file");
            bool success = await converterService.ConvertAsync(asmFile);

            if (!success)
            {
                logger.Log(LogLevel.Error, "Could not convert .asm file");
                return false;
            }

            success = await yosysService.CompileAsync(project, fpga);
            return success;
        }
        catch (Exception e)
        {
            logger.Error(e.Message, e);
            return false;
        }
    }

    public async Task<bool> FitAsync(UniversalFpgaProjectRoot project, FpgaModel fpga)
    {
        return await yosysService.FitAsync(project, fpga);
    }

    public async Task<bool> AssembleAsync(UniversalFpgaProjectRoot project, FpgaModel fpga)
    {
        return await yosysService.AssembleAsync(project, fpga);
    }


}
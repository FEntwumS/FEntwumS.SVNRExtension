using FEntwumS.SVNRExtension.Services;
using FEntwumS.SVNRExtension.Tools;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.ProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace FEntwumS.SVNRExtension;

public class AsmToVhdlPreCompileStep(AsmConverterService converterService, ILogger logger) : IFpgaPreCompileStep
{
    public string Name => "ASM to Vhdl converter";

    public async Task<bool> PerformPreCompileStepAsync(UniversalFpgaProjectRoot project, FpgaModel fpga)
    {
        
        try
        {
            var asmPath = SvnrSettingsHelper.GetAsmFile(project);
            if (asmPath.Equals("none"))
            {
                throw new Exception("No .asm file found");
            }

            var asmFile = new ProjectFile(asmPath, project.TopFolder!);
            var success = await converterService.ConvertAsync(asmFile);
            return success;
            
        }
        catch (Exception e)
        {
            logger.Error(e.Message, e);
            return false;
        }
    }
}
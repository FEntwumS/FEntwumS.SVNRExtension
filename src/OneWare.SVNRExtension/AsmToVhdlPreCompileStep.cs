using OneWare.Essentials.Services;
using OneWare.SVNRExtension.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.SVNRExtension;

public class AsmToVhdlPreCompileStep(AsmConverterService converterService, ILogger logger) : IFpgaPreCompileStep
{
    public string Name => "ASM to Vhdl converter";

    public async Task<bool> PerformPreCompileStepAsync(UniversalFpgaProjectRoot project, FpgaModel fpga)
    {
        try
        {
            var asmFiles = project.Files.FindAll(x => x.Extension == ".asm");
            if (asmFiles.Count == 0)
            {
                throw new Exception("No .asm file found");
            }
            if (asmFiles.Count > 1)
            {
                throw new Exception("More than one .asm file found");
            }
            
            var success = await converterService.ConvertAsync(asmFiles[0]);
            return success;
            
        }
        catch (Exception e)
        {
            logger.Error(e.Message, e);
            return false;
        }
    }
}
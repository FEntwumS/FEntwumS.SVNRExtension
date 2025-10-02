using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.Praktikum4Extension.Services;

public class AsmConverterService(ILogger logger, IOutputService outputService)
{
    
    public async Task<bool> ConvertAsync(IProjectFile file)
    {
        outputService.WriteLine("Converting .asm file...");
        int i = 0;
        try
        {
            if (file.Root is not UniversalFpgaProjectRoot root) {throw new Exception("File is not in a suitable Project");}
            
            var header =
                "library ieee;\nuse ieee.std_logic_1164.all;\n\npackage svnr_memory_image is\n    constant address_size : integer := 10;  -- ram_adddress breite\n    type mem_type is array (0 to (2**address_size)-1) of std_logic_vector(15 downto 0);\n\n    constant mem_init_image : mem_type := (\n";
            var tail = "    );\n\nend svnr_memory_image;";
            var ramValues = new string[1024];
            
            using (var asmReader = new StreamReader(file.FullPath))
            {
                for (i = 0; i < 1024; i++)
                {
                    var asmCommand = await ExtractCommandAsync(asmReader);
                    if (asmCommand.eof)
                    {
                        while (i < 1024)
                        {
                            ramValues[i] = "0000";
                            i++;
                        }

                        break;
                    }

                    if (i > asmCommand.address)
                    {
                        throw new Exception("Illegal address order: " + asmCommand.address.ToString("X") + " came after " + i.ToString("X"));
                    }

                    while (i < asmCommand.address)
                    {
                        ramValues[i] = "0000";
                        i++;
                    }

                    ramValues[i] = asmCommand.command;
                }
            }

            var projectPath = root.FullPath;
            var vhdlFilePath = Directory.GetFiles(projectPath, "mem_init_package.vhd", SearchOption.AllDirectories).First();
            if (vhdlFilePath.Length == 0)
            {
                throw new Exception("mem_init_package.vhd not found");
            }

            await using (var vhdlWriter = new StreamWriter(vhdlFilePath))
            {
                await vhdlWriter.WriteLineAsync(header);
                for (i = 0; i < ramValues.Length-1; i++)
                {
                    await vhdlWriter.WriteLineAsync($"        {i} => x\"{ramValues[i]}\",");
                }
                await vhdlWriter.WriteLineAsync($"        {i} => x\"{ramValues.Last()}\"");
                await vhdlWriter.WriteLineAsync(tail);
            }
            
        }
        catch (Exception e)
        {
            logger.Error("Conversion from Assembler to VHDL failed at line " + i + " : " + e.Message);
            return false;
        }
        
        outputService.WriteLine("Conversion completed");
        return true;
    }

    private async Task<(bool eof, int address, string command)> ExtractCommandAsync(StreamReader reader)
    {
        var commandTable = new Dictionary<string, string>()
        {
            ["NOOP"] = "10",
            ["LDM"] = "11",
            ["LDI"] = "12",
            ["LDA"] = "18",
            ["STI"] = "21",
            ["STM"] = "28",
            ["ADD"] = "30",
            ["SUB"] = "31",
            ["AND"] = "34",
            ["OR"] = "35",
            ["NOT"] = "36",
            ["XOR"] = "37",
            ["INC"] = "38",
            ["DEC"] = "39",
            ["LEFT"] = "3c",
            ["RIGT"] = "3d",
            ["JM"] = "41",
            ["JA"] = "48",
            ["JZM"] = "51",
            ["JNM"] = "52",
            ["JLM"] = "53",
            ["JZA"] = "58",
            ["JNA"] = "59",
            ["JLA"] = "5a",
            ["IN"] = "61",
            ["OUT"] = "71",
        };
        
        string? line;
        do
        {
            line = await reader.ReadLineAsync();
            if (line == null)
            {
                return (true, 0, "");
            }
            line = line.Trim();
        } 
        while (line.Length == 0 || line[0] == '#' || line[0] == ';');
        //splitString contains the memory address at index 0 and the memory content at index 1. Every other index (if they exist) contain the full or parts of the in-line comment.
        var splitString = line.Split([ ':', ';' ], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var address = Convert.ToInt16(splitString[0], 16);
        
        var opCode = splitString[1].Substring(0, splitString[1].Length - 2);
        if (commandTable.ContainsKey(opCode.ToUpper()))
        {
            opCode = commandTable[opCode.ToUpper()];
        }
        
        var operand = splitString[1].Substring(splitString[1].Length - 2);
        var command = opCode + operand;

        if (command.Length != 4)
        {
            throw new Exception("Invalid command: '" + splitString[0] + ": " + splitString[1] + "'");
        }
        
        return (false, address, command.ToLower());
    }
}
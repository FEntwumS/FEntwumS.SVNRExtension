using Avalonia.Media;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace FEntwumS.SVNRExtension.Services;

public class AsmConverterService(IOutputService outputService)
{
    
    public async Task<bool> ConvertAsync(IProjectFile file)
    {
        outputService.WriteLine("Converting '" + file.RelativePath + "' to mem_init_package.vhd");
        int i = 0;
        long readLineCounter = 0;
        try
        {
            if (file.Root is not UniversalFpgaProjectRoot root) {throw new Exception("File is not in a suitable Project");}
            
            var projectPath = root.FullPath;
            var vhdlFilePath = Directory.GetFiles(projectPath, "mem_init_package.vhd", SearchOption.AllDirectories).FirstOrDefault("");
            if (vhdlFilePath.Length == 0)
            {
                throw new Exception("mem_init_package.vhd not found");
            }
            
            var header =
                "library ieee;\nuse ieee.std_logic_1164.all;\n\npackage svnr_memory_image is\n    constant address_size : integer := 10;  -- ram_adddress breite\n    type mem_type is array (0 to (2**address_size)-1) of std_logic_vector(15 downto 0);\n\n    constant mem_init_image : mem_type := (\n";
            var tail = "    );\n\nend svnr_memory_image;";
            var ramValues = new string[1024];
            var comments = new string[1024];
            
            using (var asmReader = new StreamReader(file.FullPath))
            {
                
                for (i = 0; i < 1024; i++)
                {
                    (bool eof, int address, string command, string comment) asmCommand;
                    do
                    {
                        readLineCounter++;
                        asmCommand = await ExtractCommandAsync(asmReader);
                    } while (asmCommand.address == -1);
                    
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
                        throw new Exception("Illegal address order: " + asmCommand.address.ToString("X") + " came after " + (i-1).ToString("X"));
                    }

                    while (i < asmCommand.address)
                    {
                        ramValues[i] = "0000";
                        i++;
                    }

                    ramValues[i] = asmCommand.command;
                    comments[i] = asmCommand.comment;
                }
            }

            await using (var vhdlWriter = new StreamWriter(vhdlFilePath))
            {
                await vhdlWriter.WriteLineAsync(header);
                for (i = 0; i < ramValues.Length-1; i++)
                {
                    await vhdlWriter.WriteLineAsync($"        {i} => x\"{ramValues[i]}\", -- {comments[i]}");
                }
                await vhdlWriter.WriteLineAsync($"        {i} => x\"{ramValues.Last()}\" -- {comments.Last()}");
                await vhdlWriter.WriteLineAsync(tail);
            }
            
        }
        catch (Exception e)
        {
            outputService.WriteLine("Conversion from Assembler to VHDL failed at line " + readLineCounter + " : " + e.Message, Brushes.Red);
            return false;
        }
        
        outputService.WriteLine("Conversion completed");
        return true;
    }

    private async Task<(bool eof, int address, string command, string comment)> ExtractCommandAsync(StreamReader reader)
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

        line = await reader.ReadLineAsync();
        if (line == null)
        {
            return (true, 0, "", "");
        }

        line = line.Trim();
        if (line.Length == 0 || line[0] == '#' || line[0] == ';')
        {
            return (false, -1, "", "");
        }

        //splitString contains the memory address at index 0 and the memory content at index 1. Every other index (if they exist) contain the full or parts of the in-line comment.
        var splitString =
            line.Split([':', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var commentParts = new string[splitString.Length - 2];
        if (splitString.Length >= 2)
        {
            commentParts = splitString.Take(2..(splitString.Length)).ToArray();
            //comment = string.Join(" ", commentParts);
        }

        var address = Convert.ToInt16(splitString[0], 16);

        var opCode = splitString[1].Substring(0, splitString[1].Length - 2).ToUpper();
        var Mnemonic = "NONE";
        if (commandTable.ContainsKey(opCode))
        {
            Mnemonic = opCode;
            opCode = commandTable[opCode];
        }

        var operand = splitString[1].Substring(splitString[1].Length - 2);
        var command = opCode + operand;
        var invalidChars = false;

        foreach (var c in command)
        {
            if (!Uri.IsHexDigit(c))
            {
                invalidChars = true;
                break;
            }
        }

        if (command.Length != 4 || invalidChars)
        {
            throw new Exception("Invalid command: '" + splitString[0] + ": " + splitString[1] + "'");
        }

        string[] commentBuilder = {"Address " + splitString[0], "Value " + splitString[1], "Mnemonic : "+ Mnemonic, string.Join(" ", commentParts)};

        var comment = string.Join(", ", commentBuilder);
        
        
        return (false, address, command.ToLower(), comment);
    }
}
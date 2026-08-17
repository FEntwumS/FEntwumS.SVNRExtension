using FEntwumS.SVNRExtension.Asm.Constants;
using FEntwumS.SVNRExtension.Asm.Entities;
using FEntwumS.SVNRExtension.Elf.Entities;

namespace FEntwumS.SVNRExtension.Elf;

public static class SvnrDebugInfo
{
    private const int FirstSourceLine = 1;
    private const uint BytesPerWord = 2;

    public static IReadOnlyList<LineMapping> BuildLineMappings(
        IReadOnlyList<AssembledInstruction> instructions)
    {
        var mappings = new List<LineMapping>();

        var previousAddress = 0;
        var previousLine = FirstSourceLine;

        foreach (var instruction in instructions.Where(x => x.IsInstruction))
        {
            mappings.Add(new LineMapping(
                instruction.SourceLine - previousLine,
                (uint)(instruction.WordAddress - previousAddress)));

            previousAddress = instruction.WordAddress;
            previousLine = instruction.SourceLine;
        }

        return mappings;
    }

    public static IReadOnlyList<SvnrVariable> BuildVariables(
        IReadOnlyList<AssembledInstruction> instructions)
    {
        var mnemonicsByOperand = new Dictionary<byte, HashSet<string>>();

        foreach (var instruction in instructions)
        {
            if (instruction.Mnemonic is not { } mnemonic) continue;
            if (!SvnrInstructionSet.AddressesMemory(mnemonic)) continue;

            if (!mnemonicsByOperand.TryGetValue(instruction.Operand, out var mnemonics))
            {
                mnemonics = [];
                mnemonicsByOperand[instruction.Operand] = mnemonics;
            }

            mnemonics.Add(mnemonic);
        }

        return mnemonicsByOperand
            .OrderBy(entry => entry.Key)
            .Select(entry => new SvnrVariable(
                NameFor(entry.Key, entry.Value),
                entry.Key * BytesPerWord,
                FirstSourceLine))
            .ToList();
    }

    private static string NameFor(byte wordAddress, IReadOnlyCollection<string> mnemonics)
    {
        var isPointer = mnemonics.Any(SvnrInstructionSet.AddressesMemoryIndirectly);
        var isWritten = mnemonics.Any(SvnrInstructionSet.WritesMemory);

        var kind = (isPointer, isWritten) switch
        {
            (true, true) => "pointer",
            (true, false) => "read_only_pointer",
            (false, true) => "var",
            (false, false) => "read_only_var"
        };

        return $"{kind}_at_0x{wordAddress:x}";
    }
}

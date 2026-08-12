namespace FEntwumS.SVNRExtension.Asm.Constants;

public static class SvnrInstructionSet
{
    private static readonly Dictionary<string, byte> OpcodeByMnemonic = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NOOP"] = 0x10,
        ["LDM"] = 0x11,
        ["LDI"] = 0x12,
        ["LDA"] = 0x18,
        ["STI"] = 0x21,
        ["STM"] = 0x28,
        ["ADD"] = 0x30,
        ["SUB"] = 0x31,
        ["AND"] = 0x34,
        ["OR"] = 0x35,
        ["NOT"] = 0x36,
        ["XOR"] = 0x37,
        ["INC"] = 0x38,
        ["DEC"] = 0x39,
        ["LEFT"] = 0x3c,
        ["RIGT"] = 0x3d,
        ["JM"] = 0x41,
        ["JA"] = 0x48,
        ["JZM"] = 0x51,
        ["JNM"] = 0x52,
        ["JLM"] = 0x53,
        ["JZA"] = 0x58,
        ["JNA"] = 0x59,
        ["JLA"] = 0x5a,
        ["IN"] = 0x61,
        ["OUT"] = 0x71
    };

    private static readonly HashSet<string> MemoryOperandMnemonics = new(StringComparer.OrdinalIgnoreCase)
    {
        "LDM", "LDI", "STI", "STM", "ADD", "SUB", "AND", "OR", "XOR", "JM", "JZM", "JNM", "JLM"
    };

    private static readonly HashSet<string> IndirectMnemonics = new(StringComparer.OrdinalIgnoreCase)
    {
        "LDI", "STI"
    };

    private static readonly HashSet<string> MemoryWritingMnemonics = new(StringComparer.OrdinalIgnoreCase)
    {
        "STM"
    };

    public static bool TryGetOpcode(string mnemonic, out byte opcode)
    {
        return OpcodeByMnemonic.TryGetValue(mnemonic, out opcode);
    }

    public static bool AddressesMemory(string mnemonic)
    {
        return MemoryOperandMnemonics.Contains(mnemonic);
    }

    public static bool AddressesMemoryIndirectly(string mnemonic)
    {
        return IndirectMnemonics.Contains(mnemonic);
    }

    public static bool WritesMemory(string mnemonic)
    {
        return MemoryWritingMnemonics.Contains(mnemonic);
    }
}

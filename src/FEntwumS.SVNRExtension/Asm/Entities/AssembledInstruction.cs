namespace FEntwumS.SVNRExtension.Asm.Entities;

public readonly record struct AssembledInstruction(
    int SourceLine,
    ushort WordAddress,
    string? Mnemonic,
    byte Operand,
    ushort Value,
    string Comment)
{
    public bool IsInstruction => Mnemonic is not null;
}

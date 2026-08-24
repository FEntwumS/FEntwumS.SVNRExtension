namespace FEntwumS.SVNRExtension.Asm.Entities;

public readonly record struct AssembledInstruction(
    int SourceLine,
    ushort WordAddress,
    string? Mnemonic,
    byte Operand,
    ushort Value,
    string Comment) // wird aus der rohen Quellenzeile extrahiert, danach aber nicht genutzt -> kann raus?
{
    public bool IsInstruction => Mnemonic is not null; // andernfalls reines Datenwort
}

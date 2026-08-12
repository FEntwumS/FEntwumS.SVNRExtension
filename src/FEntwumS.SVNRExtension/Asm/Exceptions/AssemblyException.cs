namespace FEntwumS.SVNRExtension.Asm.Exceptions;

public sealed class AssemblyException(int sourceLine, string message)
    : Exception($"Zeile {sourceLine}: {message}")
{
    public int SourceLine { get; } = sourceLine;
}

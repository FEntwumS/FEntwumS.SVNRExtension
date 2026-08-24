namespace FEntwumS.SVNRExtension.Asm.Entities;

public enum AssemblySeverity
{
    Warning,
    Error
}

public readonly record struct AssemblyDiagnostic(AssemblySeverity Severity, int SourceLine, string Message)
{
    public override string ToString()
    {
        return $"{Severity} (Line {SourceLine}): {Message}"; 
    }
}

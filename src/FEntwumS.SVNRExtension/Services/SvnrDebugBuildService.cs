using FEntwumS.SVNRExtension.Asm;
using FEntwumS.SVNRExtension.Asm.Entities;
using FEntwumS.SVNRExtension.Elf;

namespace FEntwumS.SVNRExtension.Services;

public sealed record SvnrDebugArtifacts(
    string BinaryPath,
    string ElfPath,
    IReadOnlyList<AssemblyDiagnostic> Diagnostics);

public sealed class SvnrDebugBuildService
{
    private const string TemplateFile = "template.o";
    private const string DebugDirectoryName = "debug";
    private const string BuildDirectoryName = "build";

    public SvnrDebugArtifacts Build(string assemblerFilePath, string projectDirectory)
    {
        var debugDirectory = Path.Combine(projectDirectory, BuildDirectoryName, DebugDirectoryName);
        Directory.CreateDirectory(debugDirectory);

        SvnrProgram program;
        using (var reader = new StreamReader(assemblerFilePath))
        {
            program = SvnrAssembler.Assemble(reader);
        }

        var sourceFileName = Path.GetFileName(assemblerFilePath);

        // Fuer die Debug-Informationen der Pfad relativ zum Projektordner, nicht der blosse
        // Dateiname. GDB setzt die Quelldatei als DW_AT_comp_dir + Name zusammen; liegt die
        // .asm in einem Unterordner - und das ist der Normalfall, etwa asm/blink.asm -, zeigt
        // der blosse Name auf eine Datei, die es dort nicht gibt, und GDB meldet beim Halten
        // "No such file or directory". Vorwaertsschraegstriche, weil DWARF sie so vorsieht.
        var sourceRelativePath = Path.GetRelativePath(projectDirectory, assemblerFilePath)
            .Replace(Path.DirectorySeparatorChar, '/');

        var binaryPath = Path.Combine(debugDirectory,
            Path.GetFileNameWithoutExtension(sourceFileName) + ".bin");

        WriteAtomically(binaryPath, program.ToBinaryImage());

        var elfPath = new ElfGenerator().CreateElfFile(
            debugDirectory,
            ResolveTemplatePath(),
            projectDirectory,
            sourceRelativePath,
            SvnrDebugInfo.BuildLineMappings(program.Instructions),
            SvnrDebugInfo.BuildVariables(program.Instructions));

        return new SvnrDebugArtifacts(binaryPath, elfPath, program.Diagnostics);
    }

    private static string ResolveTemplatePath()
    {
        var directory = Path.GetDirectoryName(typeof(SvnrDebugBuildService).Assembly.Location)!;
        var path = Path.Combine(directory, "Assets", TemplateFile);

        return !File.Exists(path) ? throw new FileNotFoundException($"No ELF-Template-File found: {path}", path) : path;
    }

    private static void WriteAtomically(string target, byte[] content)
    {
        var temporary = Path.Combine(Path.GetDirectoryName(target)!, Path.GetRandomFileName() + ".tmp");

        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
            {
                stream.Write(content, 0, content.Length);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, target, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            throw;
        }
    }
}

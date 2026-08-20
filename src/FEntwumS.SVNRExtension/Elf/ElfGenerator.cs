using System.Buffers.Binary;

using FEntwumS.SVNRExtension.Elf.Dwarf;
using FEntwumS.SVNRExtension.Elf.Dwarf.Constants;
using FEntwumS.SVNRExtension.Elf.Entities;

namespace FEntwumS.SVNRExtension.Elf;

/// <summary>
/// Das Gegenstueck zu <c>create_elf_file()</c> aus <c>elfgen.py</c>: erzeugt aus den
/// Informationen des Assemblers eine ELF-Datei mit DWARF-Debug-Info.
/// </summary>
/// <remarks>
/// Ablauf:
/// <list type="number">
/// <item>Template lesen</item>
/// <item>DIE-Baum bauen (Maschinenadressen stehen zu diesem Zeitpunkt fest)</item>
/// <item>Debug-Sektionen aus dem Baum serialisieren</item>
/// <item>Sektionen zusammenfuegen und Offsets berechnen</item>
/// <item>in eine temporaere Datei schreiben und atomar ersetzen</item>
/// </list>
/// Schritt 5 sorgt dafuer, dass die Zieldatei immer entweder der alte oder der neue
/// Stand ist, nie eine Mischung - der Grund, warum <c>make clean</c> nicht mehr noetig ist.
/// </remarks>
public sealed class ElfGenerator
{
    private const ushort EtExec = 2;
    private const ushort Em68K = 0x04;
    private const uint EvCurrent = 1;

    private const int ShOffsetField = 16;
    private const int ShSizeField = 20;
    private const int ShLinkField = 24;

    /// <summary>Reihenfolge der Sektionen in der Ausgabedatei. Muss zum Template passen.</summary>
    private static readonly string[] SectionNames =
    {
        "", ".group", ".text", ".rel.text", ".data", ".bss",
        ".text.__x86.get_pc_thunk.ax", ".debug_info", ".rel.debug_info",
        ".debug_abbrev", ".debug_aranges", ".rel.debug_aranges", ".debug_line",
        ".rel.debug_line", ".debug_str", ".comment", ".note.GNU-stack",
        ".note.gnu.property", ".eh_frame", ".rel.eh_frame", ".symtab",
        ".strtab", ".shstrtab",
    };

    public string Producer { get; set; } = "SVNR_Assembler";

    public string SubprogramName { get; set; } = "SVNR_Programm";

    /// <summary>Groesse des adressierbaren SVNR-Bereichs.</summary>
    public uint HighPc { get; set; } = 0x800;

    public byte PointerSize { get; set; } = 4;

    /// <summary>
    /// Erzeugt <c>&lt;Quelldatei ohne Endung&gt;.elf</c> in <paramref name="debugDirectory"/>
    /// und liefert den Pfad der geschriebenen Datei.
    /// </summary>
    /// <param name="sourceRelativePath">
    /// Pfad der Quelldatei relativ zu <paramref name="projectDirectory"/>, mit
    /// Vorwaertsschraegstrichen - etwa <c>asm/blink.asm</c>. Bewusst nicht der blosse
    /// Dateiname: GDB setzt die Quelldatei als <c>DW_AT_comp_dir</c> + Name zusammen, und
    /// liegt die Datei in einem Unterordner, zeigt der blosse Name ins Leere.
    /// </param>
    /// <exception cref="InvalidDataException">Template unbrauchbar.</exception>
    /// <exception cref="IOException">Datei konnte nicht geschrieben werden.</exception>
    public string CreateElfFile(
        string debugDirectory,
        string elfTemplatePath,
        string projectDirectory,
        string sourceRelativePath,
        IReadOnlyList<LineMapping> lineMappings,
        IReadOnlyList<SvnrVariable> variables)
    {
        if (debugDirectory is null) throw new ArgumentNullException(nameof(debugDirectory));
        if (elfTemplatePath is null) throw new ArgumentNullException(nameof(elfTemplatePath));
        if (projectDirectory is null) throw new ArgumentNullException(nameof(projectDirectory));
        if (sourceRelativePath is null) throw new ArgumentNullException(nameof(sourceRelativePath));
        if (lineMappings is null) throw new ArgumentNullException(nameof(lineMappings));
        if (variables is null) throw new ArgumentNullException(nameof(variables));

        // ---- 1. Template ------------------------------------------------------
        var template = ElfTemplate.Load(elfTemplatePath, SectionNames);

        // ---- 2./3. Debug-Sektionen -------------------------------------------
        var strings = new DwarfStringTable();
        var compileUnit = BuildCompileUnit(strings, projectDirectory, sourceRelativePath, variables);

        var infoWriter = new DwarfInfoWriter(PointerSize);
        var debugInfo = infoWriter.WriteCompileUnit(compileUnit);
        var debugAbbrev = infoWriter.Abbreviations.ToBytes();

        var data = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var name in SectionNames)
        {
            data[name] = template[name].Data;
        }

        data[".debug_info"] = debugInfo;
        data[".debug_abbrev"] = debugAbbrev;
        data[".debug_str"] = strings.ToBytes();      // erst jetzt - der Baum kann Strings nachgelegt haben
        data[".debug_line"] = DebugLineWriter.Write(projectDirectory, sourceRelativePath, lineMappings);
        data[".debug_aranges"] = DebugArangesWriter.Write(HighPc, PointerSize);

        // Die Relocations stammen aus dem x86-Template, ihre Offsets zeigen in ein Layout,
        // das es nicht mehr gibt, und durch EM_68K werden ihre Typen fehlinterpretiert.
        // Sektionen bleiben leer erhalten, damit sich die Indizes in .symtab und .group
        // nicht verschieben.
        foreach (var name in SectionNames)
        {
            if (name.StartsWith(".rel", StringComparison.Ordinal))
            {
                data[name] = Array.Empty<byte>();
            }
        }

        // ---- 4. Section-Header und Offsets ------------------------------------
        var headers = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var offset = (uint)(ElfTemplate.ElfHeaderSize + SectionNames.Length * ElfTemplate.SectionHeaderSize);

        foreach (var name in SectionNames)
        {
            var header = (byte[])template[name].Header.Clone();

            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(ShOffsetField), offset);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(ShSizeField), (uint)data[name].Length);

            // sh_link von .symtab zeigt auf .strtab - vorletzte Sektion in unserer Liste.
            if (name == ".symtab")
            {
                BinaryPrimitives.WriteUInt32LittleEndian(
                    header.AsSpan(ShLinkField), (uint)(SectionNames.Length - 2));
            }

            headers[name] = header;
            offset += (uint)data[name].Length;
        }

        // ---- 5. atomar schreiben ---------------------------------------------
        var target = Path.Combine(debugDirectory,
            Path.GetFileNameWithoutExtension(sourceRelativePath) + ".elf");

        WriteAtomically(target, BuildElfHeader(), headers, data);
        return target;
    }

    private Die BuildCompileUnit(
        DwarfStringTable strings,
        string projectDirectory,
        string sourceRelativePath,
        IReadOnlyList<SvnrVariable> variables)
    {
        var compileUnit = new Die(DwTag.CompileUnit,
            DieAttribute.StringRef(DwAt.Producer, strings.Offset(Producer)),
            DieAttribute.Data1(DwAt.Language, (byte)DwLang.C99),
            DieAttribute.StringRef(DwAt.Name, strings.Offset(sourceRelativePath)),
            DieAttribute.StringRef(DwAt.CompDir, strings.Offset(projectDirectory)),
            DieAttribute.Address(DwAt.LowPc, 0),
            DieAttribute.Data4(DwAt.HighPc, HighPc),
            DieAttribute.SecOffset(DwAt.StmtList, 0));

        var baseType = compileUnit.AddChild(new Die(DwTag.BaseType,
            DieAttribute.Data1(DwAt.ByteSize, 2),
            DieAttribute.Data1(DwAt.Encoding, (byte)DwAte.Signed),
            DieAttribute.InlineString(DwAt.Name, "int16_t")));

        var subprogram = compileUnit.AddChild(new Die(DwTag.Subprogram,
            DieAttribute.FlagPresent(DwAt.External),
            DieAttribute.StringRef(DwAt.Name, strings.Offset(SubprogramName)),
            DieAttribute.Data1(DwAt.DeclFile, 1),
            DieAttribute.Data1(DwAt.DeclLine, 1),
            DieAttribute.Data1(DwAt.DeclColumn, 0),
            DieAttribute.Reference(DwAt.Type, baseType),
            DieAttribute.Address(DwAt.LowPc, 0),
            DieAttribute.Data4(DwAt.HighPc, HighPc),
            DieAttribute.ExprLoc(DwAt.FrameBase, (byte)DwOp.CallFrameCfa)));

        foreach (var variable in variables)
        {
            var location = new byte[5];
            location[0] = (byte)DwOp.Addr;
            BinaryPrimitives.WriteUInt32LittleEndian(location.AsSpan(1), variable.Address);

            subprogram.AddChild(new Die(DwTag.Variable,
                DieAttribute.InlineString(DwAt.Name, variable.Name),
                DieAttribute.Data1(DwAt.DeclFile, 1),
                DieAttribute.Data1(DwAt.DeclLine, (byte)Math.Clamp(variable.Line, 0, 255)),
                DieAttribute.Data1(DwAt.DeclColumn, 0),
                DieAttribute.Reference(DwAt.Type, baseType),
                DieAttribute.ExprLoc(DwAt.Location, location)));
        }

        return compileUnit;
    }

    private byte[] BuildElfHeader()
    {
        var header = new byte[ElfTemplate.ElfHeaderSize];

        header[0] = 0x7f;
        header[1] = (byte)'E';
        header[2] = (byte)'L';
        header[3] = (byte)'F';
        header[4] = 1;                              // ELFCLASS32
        header[5] = 1;                              // ELFDATA2LSB = 1 = LITTLE-ENDIAN KOMPLETTE ENDIANESS kommt HIER her
        header[6] = 1;                              // EV_CURRENT

        var span = header.AsSpan();
        BinaryPrimitives.WriteUInt16LittleEndian(span[16..], EtExec);
        BinaryPrimitives.WriteUInt16LittleEndian(span[18..], Em68K);
        BinaryPrimitives.WriteUInt32LittleEndian(span[20..], EvCurrent);
        BinaryPrimitives.WriteUInt32LittleEndian(span[24..], 0);                          // e_entry
        BinaryPrimitives.WriteUInt32LittleEndian(span[28..], 0);                          // e_phoff
        BinaryPrimitives.WriteUInt32LittleEndian(span[32..], ElfTemplate.ElfHeaderSize);  // e_shoff
        BinaryPrimitives.WriteUInt32LittleEndian(span[36..], 0);                          // e_flags
        BinaryPrimitives.WriteUInt16LittleEndian(span[40..], ElfTemplate.ElfHeaderSize);  // e_ehsize
        BinaryPrimitives.WriteUInt16LittleEndian(span[42..], 0);                          // e_phentsize
        BinaryPrimitives.WriteUInt16LittleEndian(span[44..], 0);                          // e_phnum
        BinaryPrimitives.WriteUInt16LittleEndian(span[46..], ElfTemplate.SectionHeaderSize);
        BinaryPrimitives.WriteUInt16LittleEndian(span[48..], (ushort)SectionNames.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(span[50..], (ushort)(SectionNames.Length - 1));

        return header;
    }

    private static void WriteAtomically(
        string target,
        byte[] elfHeader,
        IReadOnlyDictionary<string, byte[]> headers,
        IReadOnlyDictionary<string, byte[]> data)
    {
        var directory = Path.GetDirectoryName(target)!;
        var temporary = Path.Combine(directory, Path.GetRandomFileName() + ".elf.tmp");

        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
            {
                stream.Write(elfHeader, 0, elfHeader.Length);

                foreach (var name in SectionNames)
                {
                    var header = headers[name];
                    stream.Write(header, 0, header.Length);
                }

                foreach (var name in SectionNames)
                {
                    var section = data[name];
                    stream.Write(section, 0, section.Length);
                }

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

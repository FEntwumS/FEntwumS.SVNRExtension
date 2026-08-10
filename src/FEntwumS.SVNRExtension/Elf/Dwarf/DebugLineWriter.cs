using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

using FEntwumS.SVNRExtension.Entities;

namespace FEntwumS.SVNRExtension.Elf.Dwarf;

/// <summary>
/// Erzeugt die Sektion <c>.debug_line</c> - die Zuordnung von Adressen zu Quellzeilen.
/// </summary>
internal static class DebugLineWriter
{
    private const ushort DwarfVersion = 3;
    private const byte MinimumInstructionLength = 1;
    private const byte DefaultIsStmt = 1;
    private const sbyte LineBase = -5;
    private const byte LineRange = 14;
    private const byte OpcodeBase = 13;

    private const byte DW_LNS_copy = 0x01;
    private const byte DW_LNS_advance_pc = 0x02;
    private const byte DW_LNS_advance_line = 0x03;

    private static readonly byte[] StandardOpcodeLengths =
        { 0, 1, 1, 1, 1, 0, 0, 0, 1, 0, 0, 1 };

    /// <summary>DW_LNE_end_sequence: extended opcode (0x00), Laenge 1, Opcode 0x01.</summary>
    private static readonly byte[] EndSequence = { 0x00, 0x01, 0x01 };

    public static byte[] Write(string compilationDirectory, string fileName,
                               IReadOnlyList<LineMapping> lineMappings)
    {
        if (compilationDirectory is null) throw new ArgumentNullException(nameof(compilationDirectory));
        if (fileName is null) throw new ArgumentNullException(nameof(fileName));
        if (lineMappings is null) throw new ArgumentNullException(nameof(lineMappings));

        var prologue = BuildPrologue(compilationDirectory, fileName);
        var program = BuildProgram(lineMappings);

        var data = new byte[prologue.Length + program.Length];
        Buffer.BlockCopy(prologue, 0, data, 0, prologue.Length);
        Buffer.BlockCopy(program, 0, data, prologue.Length, program.Length);

        // unit_length @0: alles nach diesem Feld
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0), (uint)(data.Length - 4));
        // header_length @6: alles nach diesem Feld bis zum Beginn des Programms
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(6), (uint)(prologue.Length - 10));

        return data;
    }

    private static byte[] BuildPrologue(string compilationDirectory, string fileName)
    {
        using var stream = new MemoryStream();

        stream.Write(new byte[4], 0, 4);                       // unit_length (Platzhalter)
        WriteUInt16(stream, DwarfVersion);
        stream.Write(new byte[4], 0, 4);                       // header_length (Platzhalter)
        stream.WriteByte(MinimumInstructionLength);
        stream.WriteByte(DefaultIsStmt);
        stream.WriteByte(unchecked((byte)LineBase));
        stream.WriteByte(LineRange);
        stream.WriteByte(OpcodeBase);
        stream.Write(StandardOpcodeLengths, 0, StandardOpcodeLengths.Length);

        WriteCString(stream, compilationDirectory);            // include_directories
        stream.WriteByte(0);                                   // Ende der Verzeichnisliste

        WriteCString(stream, fileName);                        // file_names
        stream.WriteByte(0);                                   // directory_index
        stream.WriteByte(0);                                   // mtime
        stream.WriteByte(0);                                   // Dateilaenge
        stream.WriteByte(0);                                   // Ende der Dateiliste

        return stream.ToArray();
    }

    private static byte[] BuildProgram(IReadOnlyList<LineMapping> lineMappings)
    {
        using var stream = new MemoryStream();

        foreach (var mapping in lineMappings)
        {
            stream.WriteByte(DW_LNS_advance_line);
            Write(stream, Leb128.Signed(mapping.LineIncrement));

            stream.WriteByte(DW_LNS_advance_pc);
            Write(stream, Leb128.Unsigned(mapping.AddressIncrement));

            stream.WriteByte(DW_LNS_copy);
        }

        // Eine Zeile gilt fuer [eigene Adresse, naechste Adresse). Ohne diesen Vorschub
        // laege end_sequence auf derselben Adresse wie die letzte Instruktion, ihr Bereich
        // waere leer und GDB meldet fuer die letzte Adresse "No source available".
        stream.WriteByte(DW_LNS_advance_pc);
        Write(stream, Leb128.Unsigned(1));

        Write(stream, EndSequence);

        return stream.ToArray();
    }

    private static void Write(Stream stream, byte[] bytes) => stream.Write(bytes, 0, bytes.Length);

    private static void WriteUInt16(Stream stream, ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteCString(Stream stream, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
        stream.WriteByte(0);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FEntwumS.SVNRExtension.Elf.Dwarf;

/// <summary>
/// Die Sektion <c>.debug_str</c>: ein Block nullterminierter Strings, auf die
/// <see cref="DwForm.Strp"/>-Attribute per Offset verweisen.
/// </summary>
/// <remarks>
/// Der erste Eintrag ist ein einzelnes Nullbyte, damit Offset 0 den leeren String
/// bezeichnet. Gleiche Strings werden dedupliziert.
/// Wichtig: <see cref="ToBytes"/> erst aufrufen, nachdem der komplette DIE-Baum
/// gebaut ist - jedes <see cref="Offset"/> kann die Tabelle noch verlaengern.
/// </remarks>
internal sealed class DwarfStringTable
{
    private readonly MemoryStream _data = new();
    private readonly Dictionary<string, uint> _offsets = new(StringComparer.Ordinal);

    public DwarfStringTable()
    {
        _data.WriteByte(0);
    }

    /// <summary>Legt den String an (falls noch nicht vorhanden) und liefert seinen Offset.</summary>
    public uint Offset(string text)
    {
        if (text is null) throw new ArgumentNullException(nameof(text));

        if (_offsets.TryGetValue(text, out var existing)) return existing;

        var offset = (uint)_data.Length;
        var bytes = Encoding.UTF8.GetBytes(text);
        _data.Write(bytes, 0, bytes.Length);
        _data.WriteByte(0);

        _offsets[text] = offset;
        return offset;
    }

    public byte[] ToBytes() => _data.ToArray();
}

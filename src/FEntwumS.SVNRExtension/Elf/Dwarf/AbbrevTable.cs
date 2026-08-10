using System.Collections.Generic;
using System.IO;

namespace FEntwumS.SVNRExtension.Elf.Dwarf;

/// <summary>
/// Die Sektion <c>.debug_abbrev</c>: vergibt pro eindeutiger
/// <see cref="AbbrevSignature"/> einen Abbreviation-Code.
/// </summary>
/// <remarks>
/// Der Grund, warum diese Klasse existiert: In der urspruenglichen Fassung wurde
/// <c>.debug_abbrev</c> unveraendert aus dem Template uebernommen, waehrend
/// <c>.debug_info</c> neu erzeugt wurde. Hier entstehen beide aus demselben Baum -
/// ein Verweis auf einen Code, den die Tabelle nicht enthaelt, ist nicht mehr
/// konstruierbar.
/// </remarks>
internal sealed class AbbrevTable
{
    private readonly Dictionary<AbbrevSignature, int> _codes = new();
    private readonly List<AbbrevSignature> _entries = new();

    /// <summary>Liefert den Code fuer dieses DIE und legt ihn bei Bedarf neu an.</summary>
    public int CodeFor(Die die)
    {
        var signature = die.Signature;
        if (_codes.TryGetValue(signature, out var code)) return code;

        code = _entries.Count + 1;          // Code 0 ist der Terminator
        _codes[signature] = code;
        _entries.Add(signature);
        return code;
    }

    public int Count => _entries.Count;

    public byte[] ToBytes()
    {
        using var stream = new MemoryStream();

        for (var i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];

            Write(stream, Leb128.Unsigned((ulong)(i + 1)));
            Write(stream, Leb128.Unsigned((ulong)entry.Tag));
            stream.WriteByte(entry.HasChildren ? (byte)1 : (byte)0);

            foreach (var attribute in entry.Attributes)
            {
                Write(stream, Leb128.Unsigned((ulong)attribute.At));
                Write(stream, Leb128.Unsigned((ulong)attribute.Form));
            }

            stream.WriteByte(0);            // Ende der Attributliste
            stream.WriteByte(0);
        }

        stream.WriteByte(0);                // Ende der Tabelle
        return stream.ToArray();
    }

    private static void Write(Stream stream, byte[] bytes) => stream.Write(bytes, 0, bytes.Length);
}

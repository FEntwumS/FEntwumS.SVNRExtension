using System.Collections.Generic;

namespace FEntwumS.SVNRExtension.Elf.Dwarf;

/// <summary>
/// LEB128-Kodierung, wie DWARF sie fuer Abbrev-Codes, Blocklaengen und die
/// Opcodes des Line Number Program verwendet.
/// </summary>
internal static class Leb128
{
    /// <summary>Unsigned LEB128.</summary>
    public static byte[] Unsigned(ulong value)
    {
        var bytes = new List<byte>(10);
        do
        {
            var b = (byte)(value & 0x7f);
            value >>= 7;
            if (value != 0) b |= 0x80;
            bytes.Add(b);
        }
        while (value != 0);
        return bytes.ToArray();
    }

    /// <summary>
    /// Signed LEB128. Wichtig fuer <see cref="LineMapping.LineIncrement"/>: die
    /// Python-Fassung hat hier ein einzelnes Byte geschrieben und ist bei
    /// Zeilenspruengen ueber 127 gescheitert.
    /// </summary>
    public static byte[] Signed(long value)
    {
        var bytes = new List<byte>(10);
        while (true)
        {
            var b = (byte)(value & 0x7f);
            value >>= 7;

            var signBitSet = (b & 0x40) != 0;
            var done = (value == 0 && !signBitSet) || (value == -1 && signBitSet);

            bytes.Add(done ? b : (byte)(b | 0x80));
            if (done) return bytes.ToArray();
        }
    }

    /// <summary>Laenge der ULEB128-Kodierung, ohne sie zu erzeugen (fuer den Layout-Durchlauf).</summary>
    public static int UnsignedLength(ulong value)
    {
        var length = 0;
        do
        {
            value >>= 7;
            length++;
        }
        while (value != 0);
        return length;
    }
}

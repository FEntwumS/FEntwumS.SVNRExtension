namespace FEntwumS.SVNRExtension.Elf.Dwarf.Constants;

/// <summary>
/// DWARF-Kodierungsformen (DW_FORM_*) - bestimmen, wie der Wert eines Attributs
/// in <c>.debug_info</c> kodiert wird.
/// </summary>
internal enum DwForm : ushort
{
    Addr = 0x01,
    Block2 = 0x03,
    Block4 = 0x04,
    Data2 = 0x05,
    Data4 = 0x06,
    Data8 = 0x07,

    /// <summary>Nullterminierter String direkt im DIE.</summary>
    String = 0x08,

    Block = 0x09,
    Block1 = 0x0a,
    Data1 = 0x0b,
    Flag = 0x0c,
    Sdata = 0x0d,

    /// <summary>Offset in <c>.debug_str</c>.</summary>
    Strp = 0x0e,

    Udata = 0x0f,
    RefAddr = 0x10,
    Ref1 = 0x11,
    Ref2 = 0x12,

    /// <summary>4-Byte-Verweis auf ein DIE derselben Compilation Unit.</summary>
    Ref4 = 0x13,

    Ref8 = 0x14,
    RefUdata = 0x15,
    Indirect = 0x16,

    /// <summary>Offset in eine andere Debug-Sektion (DWARF 4).</summary>
    SecOffset = 0x17,

    /// <summary>ULEB128-Laenge, gefolgt von einem DWARF-Ausdruck.</summary>
    ExprLoc = 0x18,

    /// <summary>Flag ohne Datenbytes - die Anwesenheit im Abbrev ist der Wert.</summary>
    FlagPresent = 0x19,

    RefSig8 = 0x20,
}

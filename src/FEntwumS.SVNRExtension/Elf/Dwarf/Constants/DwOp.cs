namespace FEntwumS.SVNRExtension.Elf.Dwarf.Constants;

/// <summary>
/// DWARF-Ausdrucksoperationen (DW_OP_*) fuer <see cref="DwForm.ExprLoc"/>-Bloecke.
/// </summary>
internal enum DwOp : byte
{
    /// <summary>Absolute Adresse, gefolgt von einem Wert in Pointergroesse.</summary>
    Addr = 0x03,

    /// <summary>Offset relativ zur Frame-Base, gefolgt von SLEB128.</summary>
    Fbreg = 0x91,

    /// <summary>Canonical Frame Address - ueblich fuer <see cref="DwAt.FrameBase"/>.</summary>
    CallFrameCfa = 0x9c,
}

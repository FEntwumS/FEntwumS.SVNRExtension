namespace FEntwumS.SVNRExtension.Sbdp.Constants;

/// <summary>
/// Fehlercodes, gemeldet als Wert eines <see cref="SbdpType.Error" />-Pakets.
/// </summary>
public enum SvnrError : ushort
{
    /// <summary>Es kamen weniger Worte als die RAM-Groesse.</summary>
    RamNotFull = 0x0011,

    /// <summary>Es kamen mehr Worte als die RAM-Groesse.</summary>
    RamOverflow = 0x0012,

    /// <summary>
    /// Kein Breakpoint-Slot frei. Die Hardware haelt maximal 16.
    /// </summary>
    /// <remarks>
    /// Zweideutig: <c>decoder.vhd:437-445</c> sendet diesen Code auch dann, wenn der Zustand
    /// abgefragt wird, waehrend <c>z_ADDING_BP</c> noch auf <c>i_bp_edit_done</c> wartet. Ein
    /// <c>NoSpace</c> heisst also entweder "wirklich voll" oder "zu frueh gefragt".
    /// </remarks>
    NoSpace = 0x0015,

    BreakpointNotFound = 0x0016
}

namespace FEntwumS.SVNRExtension.Sbdp.Constants;

/// <summary>
/// Zustaende der Decoder-FSM, gemeldet als Wert eines <see cref="SbdpType.Status" />-Pakets.
/// </summary>
/// <remarks>
/// <c>Z_UPLOAD_TO_RAM = 2</c> und <c>Z_RESET = 7</c> aus <c>bootloader_protocol.py</c> fehlen hier
/// absichtlich: Es sind keine Statuswerte, die Hardware sendet sie nie.
/// </remarks>
public enum SvnrState : ushort
{
    PowerOn = 0x0001,
    RamFullOk = 0x0003,
    Running = 0x0004,
    DebugInit = 0x0005,
    DebugRunning = 0x0006,

    /// <summary>Angehalten - durch Breakpoint, Halt, Step oder Programmende.</summary>
    Halted = 0x000a,

    BreakpointAdded = 0x000c,
    BreakpointDeleted = 0x000d
}

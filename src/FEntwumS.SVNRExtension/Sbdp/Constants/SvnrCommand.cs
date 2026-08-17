namespace FEntwumS.SVNRExtension.Sbdp.Constants;

/// <summary>
/// Kommando-IDs, gesendet als Wert eines <see cref="SbdpType.Command" />-Pakets.
/// </summary>
/// <remarks>
/// <c>write_alu = 0x0209</c> aus dem Python-Code fehlt absichtlich: Die ALU-Flags sind read-only,
/// <c>decoder.vhd</c> kennt kein <c>010209</c>.
/// </remarks>
public enum SvnrCommand : ushort
{
    Execute = 0x0001,
    SendProgram = 0x0002,
    SwitchPowerOn = 0x0004,
    RequestState = 0x0005,
    SwitchDebug = 0x0006,
    Reset = 0x0007,
    Halt = 0x0008,
    Step = 0x0009,

    ReadProgramCounter = 0x0200,
    ReadBefehlsregister = 0x0201,
    ReadAkku = 0x0202,
    ReadHilfsregister = 0x0203,
    ReadAluFlags = 0x0204,

    WriteProgramCounter = 0x0205,
    WriteBefehlsregister = 0x0206,
    WriteAkku = 0x0207,
    WriteHilfsregister = 0x0208
}

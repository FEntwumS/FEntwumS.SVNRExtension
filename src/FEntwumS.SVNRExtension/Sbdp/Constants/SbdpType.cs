namespace FEntwumS.SVNRExtension.Sbdp.Constants;

/// <summary>
/// Pakettyp eines SBDP-2-Rahmens (erstes Byte).
/// </summary>
/// <remarks>
/// Belegt durch <c>decoder.vhd</c> und Anhang .2 der Bachelorarbeit Dolata, nicht durch
/// <c>bootloader_protocol.py</c> - der Python-Code fuehrt Konstanten, die die Hardware nicht
/// dekodiert.
/// </remarks>
public enum SbdpType : byte
{
    /// <summary>FPGA -> Host. Wert ist ein <see cref="SvnrState" />.</summary>
    Status = 0x00,

    /// <summary>Host -> FPGA. Wert ist ein <see cref="SvnrCommand" />.</summary>
    Command = 0x01,

    /// <summary>Host -> FPGA. Ein RAM-Wort beim Upload.</summary>
    ProgramData = 0x02,

    /// <summary>FPGA -> Host. Wert ist ein <see cref="SvnrError" />.</summary>
    Error = 0x03,

    /// <summary>Host -> FPGA. Wert ist die Wortadresse.</summary>
    AddBreakpoint = 0x04,

    /// <summary>Host -> FPGA. Wert ist die Wortadresse.</summary>
    DeleteBreakpoint = 0x05,

    /// <summary>Beide Richtungen: Antwort beim Lesen, Wert beim Schreiben.</summary>
    RegisterData = 0x06,

    /// <summary>Host -> FPGA. Wert ist die Wortadresse.</summary>
    ReadRamAddress = 0x07,

    /// <summary>Host -> FPGA. Wert ist die Wortadresse.</summary>
    WriteRamAddress = 0x08,

    /// <summary>Beide Richtungen: Antwort beim Lesen, Wert beim Schreiben.</summary>
    RamData = 0x09
}

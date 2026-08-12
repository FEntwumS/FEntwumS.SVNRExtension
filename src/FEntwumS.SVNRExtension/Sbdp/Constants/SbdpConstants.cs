namespace FEntwumS.SVNRExtension.Sbdp.Constants;

/// <summary>
/// Feste Groessen des Protokolls.
/// </summary>
public static class SbdpConstants
{
    /// <summary>Ein Rahmen ist immer genau 3 Byte: Typ, Highbyte, Lowbyte.</summary>
    public const int PacketSize = 3;

    /// <summary>Wortanzahl des SVNR-RAM. Bestimmt die Laenge eines gueltigen Uploads.</summary>
    public const int RamSize = 1024;

    /// <summary>Groesse eines vollstaendigen Programmabbilds in Byte (zwei Byte je Wort).</summary>
    public const int ImageSize = RamSize * 2;

    /// <summary>Maximale Anzahl gleichzeitiger Breakpoints in der Hardware.</summary>
    /// <remarks>
    /// <c>breakpoint_controller.vhd:31</c>. GDB weiss davon nichts und setzt bereitwillig den 17.;
    /// die Antwort ist dann <see cref="SvnrError.NoSpace" />.
    /// </remarks>
    public const int MaxBreakpoints = 16;

    /// <summary>Baudrate der UART-Verbindung.</summary>
    public const int BaudRate = 115200;
}

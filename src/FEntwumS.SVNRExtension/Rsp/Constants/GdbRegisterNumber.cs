namespace FEntwumS.SVNRExtension.Rsp.Constants;

/// <summary>
/// Registernummern des RSP-Protokolls: die Reihenfolge, in der die Register in
/// <c>Assets/target.xml</c> stehen.
/// </summary>
/// <remarks>
/// Nicht zu verwechseln mit der Nummerierung, die GDB intern fuehrt und in
/// <c>maint print registers</c> oder <c>-data-list-register-names</c> zeigt. Dort schiebt
/// der m68k-Kern <c>ps</c> und <c>pc</c> auf 16 und 17 und haengt alles Unbekannte hinter
/// elf Platzhalter fuer die FPU - <c>Akku</c> liegt intern auf 29. Fuer das <c>g</c>-Paket
/// und fuer <c>p</c>/<c>P</c> zaehlt aber allein die Protokollnummer, und die folgt der
/// XML-Reihenfolge. Die Platzhalter haben Groesse 0 und stehen gar nicht im Paket.
/// <para>
/// Nachgemessen an einer Mitschrift mit <c>set debug remote 1</c>: das <c>g</c>-Paket ist
/// 88 Byte lang, GDB liest <c>pc</c> bei Offset 84 und <c>ps</c> bei 80 - also hinter den
/// acht SVNR-Registern ab Offset 64, genau wie hier nummeriert.
/// </para>
/// </remarks>
public static class GdbRegisterNumber
{
    public const int FirstSvnrRegister = 0x10;
    public const int LastSvnrRegister = 0x16;

    public const int Akku = 0x10;
    public const int Programmzaehler = 0x11;
    public const int Befehlsregister = 0x12;
    public const int Hilfsregister = 0x13;
    public const int AluFlagSmallerZero = 0x14;
    public const int AluFlagGreaterZero = 0x15;
    public const int AluFlagEqualZero = 0x16;
    public const int SvnrReset = 0x17;
    public const int ProgramStatus = 0x18;
    public const int InstructionPointer = 0x19;

    public const int UnusedM68kRegisterCount = 16;
}

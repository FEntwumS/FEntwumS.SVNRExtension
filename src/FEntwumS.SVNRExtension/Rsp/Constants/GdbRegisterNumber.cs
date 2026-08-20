namespace FEntwumS.SVNRExtension.Rsp.Constants;

/// <summary>
/// Registernummern, wie GDB sie aus <c>Assets/target.xml</c> vergibt.
/// </summary>
/// <remarks>
/// Nicht die Reihenfolge der XML-Datei. Fuer die Standard-Feature-Bezeichnung
/// <c>org.gnu.gdb.coldfire.core</c> nimmt GDB seine eigene m68k-Tabelle - d0-d7, a0-a5,
/// fp, sp, ps, pc als 0..17 -, haelt danach elf Plaetze fuer die FPU-Register frei, die
/// die Beschreibung nicht liefert (18..28, Groesse 0, kein Byte im g-Paket), und haengt
/// erst dahinter die ihm unbekannten Register in XML-Reihenfolge an.
/// <para>
/// Nachgemessen am ausgelieferten Binary, nicht hergeleitet:
/// <c>gdb --batch -ex 'set architecture m68k' -ex 'set tdesc filename target.xml'
/// -ex 'maint print registers'</c>.
/// </para>
/// </remarks>
public static class GdbRegisterNumber
{
    public const int ProgramStatus = 16;
    public const int InstructionPointer = 17;

    public const int FirstSvnrRegister = 29;
    public const int LastSvnrRegister = 36;

    public const int Akku = 29;
    public const int Programmzaehler = 30;
    public const int Befehlsregister = 31;
    public const int Hilfsregister = 32;
    public const int AluFlagSmallerZero = 33;
    public const int AluFlagGreaterZero = 34;
    public const int AluFlagEqualZero = 35;
    public const int SvnrReset = 36;

    /// <summary>Die Kernregister d0-d7, a0-a5, fp und sp - im g-Paket je vier Byte Null.</summary>
    public const int UnusedM68kRegisterCount = 16;
}
